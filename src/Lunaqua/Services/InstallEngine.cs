using Lunaqua.Domain;
using Lunaqua.Infrastructure.OpenList;
using Serilog;

namespace Lunaqua.Services;

/// <summary>安装/更新的阶段进度。</summary>
/// <param name="Stage">阶段名（下载 / 落位 / 校验）。</param>
/// <param name="Done">已完成数量。</param>
/// <param name="Total">总数。</param>
public sealed record InstallProgress(string Stage, int Done, int Total);

/// <summary>游戏正在运行时的拒绝操作。</summary>
public sealed class GameRunningException() : Exception("游戏正在运行，先关掉游戏再操作。");

/// <summary>
/// 事务式安装/更新/回退/卸载/启停的唯一入口（规格书 §6.2、§7.2–§7.4）。
/// </summary>
public sealed class InstallEngine
{
    private readonly OpenListClient _client;
    private readonly ModTypeStrategies _strategies;
    private readonly CredentialStore _credentials;
    private readonly CacheStore _cache;
    private readonly IGameProcessDetector _processDetector;
    private readonly ILogger _log;

    public InstallEngine(
        OpenListClient client,
        ModTypeStrategies strategies,
        CredentialStore credentials,
        CacheStore cache,
        IGameProcessDetector processDetector,
        ILogger? log = null)
    {
        _client = client;
        _strategies = strategies;
        _credentials = credentials;
        _cache = cache;
        _processDetector = processDetector;
        _log = log ?? Log.ForContext<InstallEngine>();
    }

    /// <summary>安装或就地更新（差集删除旧版多余文件）。</summary>
    public async Task<InstalledMod> InstallAsync(
        GameContext game,
        string directoryName,
        MetadataInfo meta,
        ModVersion version,
        IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureGameNotRunning();

        var strategy = _strategies.Get(meta.Type);
        var errors = strategy.Validate(meta, version);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"这个 mod 不能安装：{string.Join("；", errors)}");
        }

        var plan = strategy.PlanInstall(game, meta, version);
        var existing = _credentials.Read(plan.CredentialPath);

        // 1. 全部文件先落到缓存并校验 sha256
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var done = 0;
        foreach (var target in plan.Targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new InstallProgress("下载", done, plan.Targets.Count));
            sources[target.DestinationPath] = await EnsureCachedAsync(directoryName, target, cancellationToken);
            done++;
        }

        progress?.Report(new InstallProgress("落位", 0, plan.Targets.Count));

        // 2. 事务：备份 → 落位 → 差集删除 → 写凭证
        var transaction = NewTransaction(game);
        try
        {
            foreach (var target in plan.Targets)
            {
                transaction.Replace(sources[target.DestinationPath], target.DestinationPath);
            }

            RemoveObsoleteFiles(game, strategy, meta.Id, existing, plan, transaction);

            var credential = new InstalledMod(
                InstalledMod.CurrentSchema,
                meta.Id,
                meta.Type,
                meta.Guid,
                version.Version.ToString(),
                ModSource.Official,
                true,
                [.. version.Files.Select(file => new InstalledFile(file.Path, file.Sha256, file.Size))],
                DateTimeOffset.Now,
                existing?.IgnoredVersion);

            _credentials.Write(plan.CredentialPath, credential);
            transaction.Commit();

            progress?.Report(new InstallProgress("落位", plan.Targets.Count, plan.Targets.Count));
            _log.Information("已安装 {Id} v{Version}", meta.Id, version.Version);
            return credential;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "安装 {Id} v{Version} 失败，开始回滚", meta.Id, version.Version);
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>卸载：按凭证删文件与凭证（先备份）。</summary>
    public void Uninstall(GameContext game, InstalledMod installed)
    {
        EnsureGameNotRunning();

        var strategy = _strategies.Get(installed.Type);
        var root = strategy.ResolveInstallRoot(game, installed.Id);
        var credentialPath = ResolveCredentialPath(game, strategy, installed);

        var transaction = NewTransaction(game);
        try
        {
            foreach (var file in installed.Files)
            {
                transaction.Remove(Path.Combine(root, ToLocalPath(file.Path)));
            }

            transaction.Commit();

            if (strategy.CredentialInsideInstallRoot)
            {
                DeleteDirectory(root);
            }
            else
            {
                _credentials.Delete(credentialPath);
                DeleteDirectoryIfEmpty(Path.GetDirectoryName(credentialPath));
            }

            _log.Information("已卸载 {Id}", installed.Id);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>启停：BepInEx 类按文件搬移，UVFS 整目录搬移。</summary>
    public InstalledMod SetEnabled(GameContext game, InstalledMod installed, bool enabled)
    {
        EnsureGameNotRunning();

        if (installed.Enabled == enabled)
        {
            return installed;
        }

        var strategy = _strategies.Get(installed.Type);
        var root = strategy.ResolveInstallRoot(game, installed.Id);
        var disabledRoot = strategy.ResolveDisabledRoot(game, installed.Id);

        if (strategy.CredentialInsideInstallRoot)
        {
            MoveTree(enabled ? disabledRoot : root, enabled ? root : disabledRoot);
        }
        else if (enabled)
        {
            foreach (var file in installed.Files)
            {
                MoveFile(Path.Combine(disabledRoot, ToLocalPath(file.Path)), Path.Combine(root, ToLocalPath(file.Path)));
            }
        }
        else
        {
            foreach (var file in installed.Files)
            {
                MoveFile(Path.Combine(root, ToLocalPath(file.Path)), Path.Combine(disabledRoot, ToLocalPath(file.Path)));
            }
        }

        if (enabled)
        {
            // 启用后把空的禁用目录清掉，避免留下空壳
            DeleteIfNoFiles(disabledRoot);
        }

        var updated = installed with { Enabled = enabled };
        _credentials.Write(ResolveCredentialPath(game, strategy, updated), updated);

        _log.Information("{Id} 已{State}", installed.Id, enabled ? "启用" : "禁用");
        return updated;
    }

    /// <summary>磁盘实况优先：文件在启用位置才算启用。</summary>
    public bool DetectEnabled(GameContext game, InstalledMod installed)
    {
        var strategy = _strategies.Get(installed.Type);
        var root = strategy.ResolveInstallRoot(game, installed.Id);
        var disabledRoot = strategy.ResolveDisabledRoot(game, installed.Id);

        if (strategy.CredentialInsideInstallRoot)
        {
            return Directory.Exists(root) && !Directory.Exists(disabledRoot);
        }

        var first = installed.Files.FirstOrDefault();
        if (first is null)
        {
            return installed.Enabled;
        }

        var relative = ToLocalPath(first.Path);
        return File.Exists(Path.Combine(root, relative)) && !File.Exists(Path.Combine(disabledRoot, relative));
    }

    private void EnsureGameNotRunning()
    {
        if (_processDetector.IsGameRunning())
        {
            throw new GameRunningException();
        }
    }

    private async Task<string> EnsureCachedAsync(string directoryName, InstallTarget target, CancellationToken cancellationToken)
    {
        if (_cache.TryGet(target.Sha256, target.Size, out var cached))
        {
            return cached;
        }

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = await _client.GetFileAsync($"/{directoryName}/{target.RemotePath}", cancellationToken);
            try
            {
                return await _cache.StoreAsync(
                    target.Sha256,
                    target.Size,
                    (stream, token) => _client.DownloadAsync(info.RawUrl, stream, null, token),
                    cancellationToken);
            }
            catch (InvalidDataException ex) when (attempt < 2)
            {
                _log.Warning("下载校验失败，重试一次：{Message}", ex.Message);
            }
        }
    }

    private static void RemoveObsoleteFiles(
        GameContext game,
        IModTypeStrategy strategy,
        string modId,
        InstalledMod? existing,
        InstallPlan plan,
        FileTransaction transaction)
    {
        if (existing is null)
        {
            return;
        }

        var root = strategy.ResolveInstallRoot(game, modId);
        var newPaths = plan.Targets.Select(target => target.RemotePath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var old in existing.Files.Where(file => !newPaths.Contains(file.Path)))
        {
            transaction.Remove(Path.Combine(root, ToLocalPath(old.Path)));
        }
    }

    private static string ResolveCredentialPath(GameContext game, IModTypeStrategy strategy, InstalledMod installed) =>
        strategy.CredentialInsideInstallRoot
            ? Path.Combine(strategy.ResolveInstallRoot(game, installed.Id), "info.json")
            : strategy.ResolveCredentialPath(game, installed.Id);

    private static FileTransaction NewTransaction(GameContext game) =>
        new(Path.Combine(game.BackupDirectory, DateTime.Now.ToString("yyyyMMdd-HHmmss")), game.GameDirectory);

    private static string ToLocalPath(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    private static void MoveFile(string source, string destination)
    {
        if (!File.Exists(source))
        {
            return;
        }

        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Move(source, destination, overwrite: true);
    }

    private static void MoveTree(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        Directory.Move(source, destination);
    }

    private void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, recursive: true);
        _log.Debug("已删除目录 {Path}", path);
    }

    private static void DeleteIfNoFiles(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        if (Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Any())
        {
            return;
        }

        Directory.Delete(path, recursive: true);
    }

    private void DeleteDirectoryIfEmpty(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return;
        }

        if (!Directory.EnumerateFileSystemEntries(path).Any())
        {
            Directory.Delete(path);
        }
    }
}
