using System.Text.Json;
using Lunaqua.Domain;
using Serilog;

namespace Lunaqua.Services;

/// <summary>一次代装的结果。</summary>
/// <param name="Success">是否成功。</param>
/// <param name="Message">给玩家看的中文说明。</param>
/// <param name="BackupDirectory">备份目录（回滚用）。</param>
/// <param name="LogExcerpt">验证时读到的 BepInEx 日志片段。</param>
/// <param name="DeployedFiles">落位的文件。</param>
public sealed record BepInExInstallResult(
    bool Success,
    string Message,
    string? BackupDirectory = null,
    string? LogExcerpt = null,
    IReadOnlyList<string>? DeployedFiles = null);

/// <summary>本地记录的代装状态。</summary>
/// <param name="Schema">结构版本。</param>
/// <param name="Version">BepInEx 版本。</param>
/// <param name="InstalledAt">安装时间。</param>
/// <param name="BackupDirectory">备份目录。</param>
/// <param name="Files">落位文件（相对游戏根）。</param>
public sealed record BepInExInstallState(
    int Schema,
    string Version,
    DateTimeOffset InstalledAt,
    string BackupDirectory,
    IReadOnlyList<string> Files)
{
    public const int CurrentSchema = 1;
}

/// <summary>
/// BepInEx 全自动代装（规格书 §8 第 2–3 步）：解包校验 → 备份 → 部署 → 跑游戏验证 → 失败回滚。
/// </summary>
public sealed class BepInExInstaller
{
    private static readonly JsonSerializerOptions StateOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IBepInExPackSource _source;
    private readonly IGameLauncher _launcher;
    private readonly BepInExPackManifest _manifest;
    private readonly ILogger _log;

    public BepInExInstaller(
        IBepInExPackSource source,
        IGameLauncher launcher,
        BepInExPackManifest manifest,
        ILogger? log = null)
    {
        _source = source;
        _launcher = launcher;
        _manifest = manifest;
        _log = log ?? Log.ForContext<BepInExInstaller>();
    }

    public BepInExPackManifest Manifest => _manifest;

    /// <summary>BepInEx 是否已经装好（核心 dll + doorstop 都在）。</summary>
    public static bool IsInstalled(GameContext game) =>
        File.Exists(Path.Combine(game.GameDirectory, "winhttp.dll"))
        && File.Exists(Path.Combine(game.GameDirectory, "BepInEx", "core", "BepInEx.dll"));

    public BepInExInstallState? ReadState(GameContext game)
    {
        var path = StatePath(game);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BepInExInstallState>(File.ReadAllText(path), StateOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "BepInEx 安装状态读取失败：{Path}", path);
            return null;
        }
    }

    public async Task<BepInExInstallResult> InstallAsync(
        GameContext game,
        bool verifyByLaunch = true,
        TimeSpan? verifyTimeout = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(game.GameDirectory))
        {
            return new BepInExInstallResult(false, "游戏目录不存在。");
        }

        var packDirectory = await _source.AcquireAsync(cancellationToken);

        var errors = SiteBepInExPackSource.VerifyPack(_manifest, packDirectory);
        if (errors.Count > 0)
        {
            return new BepInExInstallResult(false, $"BepInEx pack 校验失败：{string.Join("；", errors)}");
        }

        var backupDirectory = Path.Combine(game.BackupDirectory, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        var transaction = new FileTransaction(backupDirectory, game.GameDirectory, _log);
        var deployed = new List<string>();

        try
        {
            foreach (var file in _manifest.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var source = Path.Combine(packDirectory, file.Path.Replace('/', Path.DirectorySeparatorChar));
                var destination = Path.Combine(game.GameDirectory, file.Path.Replace('/', Path.DirectorySeparatorChar));

                transaction.Replace(source, destination);
                deployed.Add(file.Path);
            }

            transaction.Commit();
            WriteState(game, backupDirectory, deployed);
            _log.Information("BepInEx {Version} 已部署 {Count} 个文件", _manifest.Version, deployed.Count);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "部署 BepInEx 失败，回滚");
            transaction.Rollback();
            return new BepInExInstallResult(false, $"部署失败：{ex.Message}");
        }

        if (!verifyByLaunch)
        {
            return new BepInExInstallResult(true, $"已部署 BepInEx {_manifest.Version}（未做启动验证）。", backupDirectory, null, deployed);
        }

        var launch = await _launcher.LaunchAndWaitForBepInExLogAsync(
            game,
            _manifest.LogMarker,
            verifyTimeout ?? TimeSpan.FromSeconds(60),
            cancellationToken);

        if (launch.Success)
        {
            return new BepInExInstallResult(true, $"BepInEx {_manifest.Version} 装好了，游戏里已经跑起来。", backupDirectory, Excerpt(launch.LogText), deployed);
        }

        _log.Warning("启动验证失败，回滚：{Message}", launch.Message);
        RollbackFromBackup(game, backupDirectory);
        return new BepInExInstallResult(false, $"验证失败，已还原：{launch.Message}", backupDirectory, Excerpt(launch.LogText), deployed);
    }

    /// <summary>从备份目录还原（删掉新建的、复制回被覆盖的）。</summary>
    public void RollbackFromBackup(GameContext game, string backupDirectory)
    {
        var manifestPath = Path.Combine(backupDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            _log.Warning("备份清单不存在，无法回滚：{Path}", manifestPath);
            return;
        }

        BackupManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath), StateOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _log.Error(ex, "备份清单读取失败：{Path}", manifestPath);
            return;
        }

        if (manifest is null)
        {
            return;
        }

        foreach (var created in manifest.Created ?? [])
        {
            TryDelete(created);
        }

        foreach (var entry in manifest.Backups ?? [])
        {
            try
            {
                var directory = Path.GetDirectoryName(entry.Original);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(entry.Backup, entry.Original, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Error(ex, "回滚失败：{Original}", entry.Original);
            }
        }

        TryDelete(StatePath(game));
        _log.Information("已从备份还原：{Backup}", backupDirectory);
    }

    public static string StatePath(GameContext game) =>
        Path.Combine(game.LunaquaDirectory, "bepinex.json");

    private void WriteState(GameContext game, string backupDirectory, IReadOnlyList<string> files)
    {
        var path = StatePath(game);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var state = new BepInExInstallState(
            BepInExInstallState.CurrentSchema,
            _manifest.Version,
            DateTimeOffset.Now,
            backupDirectory,
            files);

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(state, StateOptions));
        File.Move(tempPath, path, overwrite: true);
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "删不掉 {Path}", path);
        }
    }

    private static string? Excerpt(string? logText) =>
        logText is null ? null : logText.Length <= 4000 ? logText : logText[..4000];

    private sealed record BackupManifestEntry(string Original, string Backup);

    private sealed record BackupManifest(
        DateTimeOffset CreatedAt,
        string? BasePath,
        List<BackupManifestEntry>? Backups,
        List<string>? Created);
}
