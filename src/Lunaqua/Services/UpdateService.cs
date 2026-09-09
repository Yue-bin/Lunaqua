using Serilog;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace Lunaqua.Services;

/// <summary>可用的更新。</summary>
/// <param name="Version">目标版本号。</param>
/// <param name="IsDowngrade">是不是往下降级（回退）。</param>
/// <param name="Notes">发布说明（Markdown）。</param>
public sealed record AppUpdate(string Version, bool IsDowngrade, string? Notes);

/// <summary>Lunaqua 自身的更新（规格书 §11）。</summary>
public interface IUpdateService
{
    /// <summary>当前版本。</summary>
    string CurrentVersion { get; }

    /// <summary>是不是 Velopack 安装版（开发运行时为 false，自更新不可用）。</summary>
    bool IsInstalled { get; }

    /// <summary>是否已有下载好、等重启的更新。</summary>
    bool IsUpdatePendingRestart { get; }

    /// <summary>检查新版本；没有返回 null。</summary>
    Task<AppUpdate?> CheckAsync(CancellationToken cancellationToken = default);

    /// <summary>下载更新。</summary>
    Task DownloadAsync(AppUpdate update, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>应用更新并重启。</summary>
    Task ApplyAndRestartAsync(AppUpdate update, CancellationToken cancellationToken = default);

    /// <summary>找「上一版」（比当前版本低的最近一个）用于回退；没有返回 null。</summary>
    Task<AppUpdate?> FindRollbackTargetAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 基于 Velopack 的实现：静态目录（<c>releases.win.json</c>）+ 差分更新。
/// 开发运行（没经过 Velopack 安装）时 UpdateManager 初始化会失败 —— 这里降级成「不可用」而不是炸掉。
/// </summary>
public sealed class VelopackUpdateService : IUpdateService
{
    /// <summary>默认更新源（自建静态目录，Caddy 托管、无 sign）。</summary>
    public const string DefaultUpdateUrl = "https://blog.monblog.top/lunaqua/updates";

    /// <summary>环境变量可覆盖更新源（联调用）。</summary>
    public const string UpdateUrlEnvVar = "LUNAQUA_UPDATE_URL";

    private readonly IUpdateSource _source;
    private readonly ILogger _log;
    private readonly Lazy<UpdateManager?> _manager;
    private readonly Dictionary<string, (VelopackAsset Asset, UpdateInfo? Info)> _known = new(StringComparer.Ordinal);

    public VelopackUpdateService(string? updateUrl = null, ILogger? log = null)
    {
        _log = log ?? Log.ForContext<VelopackUpdateService>();

        var url = updateUrl
            ?? Environment.GetEnvironmentVariable(UpdateUrlEnvVar)
            ?? DefaultUpdateUrl;

        _source = new SimpleWebSource(url);
        _manager = new Lazy<UpdateManager?>(CreateManager);

        _log.Information("更新源 {Url}，当前版本 {Version}，安装版 {Installed}", url, CurrentVersion, IsInstalled);
    }

    public string CurrentVersion =>
        _manager.Value?.CurrentVersion?.ToString()
        ?? typeof(App).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    public bool IsInstalled => _manager.Value?.IsInstalled ?? false;

    public bool IsUpdatePendingRestart => _manager.Value?.UpdatePendingRestart is not null;

    public async Task<AppUpdate?> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (Manager is not { } manager)
        {
            return null;
        }

        var info = await manager.CheckForUpdatesAsync();
        if (info is null)
        {
            return null;
        }

        var version = info.TargetFullRelease.Version.ToString();
        _known[version] = (info.TargetFullRelease, info);
        return new AppUpdate(version, info.IsDowngrade, info.TargetFullRelease.NotesMarkdown);
    }

    public async Task DownloadAsync(AppUpdate update, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (Manager is not { } manager)
        {
            throw new InvalidOperationException("当前不是 Velopack 安装版，自更新不可用。");
        }

        var (_, info) = Resolve(update);
        if (info is null)
        {
            throw new InvalidOperationException($"没有 {update.Version} 的更新信息，先检查一次更新。");
        }

        await manager.DownloadUpdatesAsync(info, percent => progress?.Report(percent), cancellationToken);
    }

    public Task ApplyAndRestartAsync(AppUpdate update, CancellationToken cancellationToken = default)
    {
        if (Manager is not { } manager)
        {
            throw new InvalidOperationException("当前不是 Velopack 安装版，自更新不可用。");
        }

        var (asset, _) = Resolve(update);
        manager.WaitExitThenApplyUpdates(asset, silent: false, restart: true, restartArgs: []);
        return Task.CompletedTask;
    }

    public async Task<AppUpdate?> FindRollbackTargetAsync(CancellationToken cancellationToken = default)
    {
        if (Manager is not { } manager)
        {
            return null;
        }

        var feed = await _source.GetReleaseFeed(
            new NullVelopackLogger(),
            manager.AppId,
            "win",
            stagingId: null,
            latestLocalRelease: null);

        var current = manager.CurrentVersion;
        var target = (feed.Assets ?? [])
            .Where(asset => asset.Version < current)
            .OrderByDescending(asset => asset.Version)
            .FirstOrDefault();

        if (target is null)
        {
            _log.Information("更新源里没有比 {Version} 更旧的版本", current);
            return null;
        }

        _known[target.Version.ToString()] = (target, null);
        return new AppUpdate(target.Version.ToString(), IsDowngrade: true, target.NotesMarkdown);
    }

    private UpdateManager? Manager => _manager.Value;

    private UpdateManager? CreateManager()
    {
        try
        {
            return new UpdateManager(_source, new UpdateOptions { AllowVersionDowngrade = true });
        }
        catch (Exception ex)
        {
            _log.Information(ex, "Velopack 不可用（多半是开发运行），自更新关闭");
            return null;
        }
    }

    private (VelopackAsset Asset, UpdateInfo? Info) Resolve(AppUpdate update) =>
        _known.TryGetValue(update.Version, out var entry)
            ? entry
            : throw new InvalidOperationException($"不认识的版本 {update.Version}，先检查一次更新。");
}
