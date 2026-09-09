using System.Text.Json;
using Lunaqua.Domain;
using Serilog;

namespace Lunaqua.Services;

/// <summary>
/// 干净判定三层（规格书 §8）：buildid 命中 → 文件哈希比对 → 本机信任基线，
/// 都不中就让调用方引导 <c>steam://validate/674940</c> 后复扫。
/// </summary>
public sealed class GameCleanCheckService
{
    private static readonly JsonSerializerOptions BaselineOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly GameFingerprintTable _table;
    private readonly ILogger _log;

    public GameCleanCheckService(GameFingerprintTable table, ILogger? log = null)
    {
        _table = table;
        _log = log ?? Log.ForContext<GameCleanCheckService>();
    }

    public GameFingerprintTable Table => _table;

    /// <param name="installation">已校验过结构的游戏安装。</param>
    /// <param name="buildId">appmanifest 里的 buildid；没有就传 null。</param>
    /// <param name="excludedPaths">
    /// 不参与比对的相对路径 —— 通常是「已经被已装 mod 覆盖」的文件（例如 UVFS 覆盖层改了 level0）。
    /// </param>
    public async Task<GameCleanCheckResult> CheckAsync(
        GameInstallation installation,
        string? buildId,
        IReadOnlySet<string>? excludedPaths = null,
        CancellationToken cancellationToken = default)
    {
        if (installation.ExecutablePath is null)
        {
            return GameCleanCheckResult.Unknown("没找到游戏可执行文件，没法判定。");
        }

        // ① buildid 命中官方表
        if (_table.FindByBuildId(buildId) is { } byBuildId)
        {
            _log.Information("干净判定：buildid {BuildId} 命中官方构建", buildId);
            return new GameCleanCheckResult(
                GameCleanStatus.Clean,
                $"buildid {buildId} 是官方构建，文件不用再比。",
                buildId,
                byBuildId.BuildId,
                []);
        }

        // ② 文件哈希比对
        var hashes = await HashKnownFilesAsync(installation, excludedPaths, cancellationToken);

        if (_table.MatchByHashes(hashes, excludedPaths) is { } byHash)
        {
            _log.Information("干净判定：文件哈希命中官方构建 {BuildId}", byHash.BuildId);
            return new GameCleanCheckResult(
                GameCleanStatus.Clean,
                $"文件哈希与官方构建 {byHash.BuildId} 一致。",
                buildId,
                byHash.BuildId,
                []);
        }

        // ③ 玩家信任过的本机基线
        var baseline = LoadBaseline(installation.GameDirectory);
        if (baseline is not null && baseline.Matches(hashes))
        {
            _log.Information("干净判定：命中本机信任基线（{CapturedAt}）", baseline.CapturedAt);
            return new GameCleanCheckResult(
                GameCleanStatus.Trusted,
                "文件与你自己信任过的本机基线一致。",
                buildId,
                null,
                []);
        }

        var mismatched = FilePaths(installation)
            .Where(path => !hashes.ContainsKey(path))
            .ToList();

        return new GameCleanCheckResult(
            GameCleanStatus.NeedsValidate,
            "这个版本不在官方指纹表里，建议先用 Steam 校验一次游戏文件。",
            buildId,
            null,
            mismatched);
    }

    /// <summary>把当前文件哈希存成「本机信任基线」。</summary>
    public async Task<TrustedBaseline> CaptureBaselineAsync(
        GameInstallation installation,
        string? buildId,
        CancellationToken cancellationToken = default)
    {
        var hashes = await HashKnownFilesAsync(installation, null, cancellationToken);
        var baseline = new TrustedBaseline(
            TrustedBaseline.CurrentSchema,
            buildId,
            Path.GetFileName(installation.ExecutablePath ?? string.Empty),
            DateTimeOffset.Now,
            hashes);

        SaveBaseline(installation.GameDirectory, baseline);
        return baseline;
    }

    public TrustedBaseline? LoadBaseline(string gameDirectory)
    {
        var path = BaselinePath(gameDirectory);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TrustedBaseline>(File.ReadAllText(path), BaselineOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "本机基线读取失败：{Path}", path);
            return null;
        }
    }

    public void SaveBaseline(string gameDirectory, TrustedBaseline baseline)
    {
        var path = BaselinePath(gameDirectory);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(baseline, BaselineOptions));
        File.Move(tempPath, path, overwrite: true);
        _log.Information("已记录本机信任基线：{Path}", path);
    }

    public static string BaselinePath(string gameDirectory) =>
        Path.Combine(gameDirectory, ".lunaqua", "trusted-baseline.json");

    /// <summary>按指纹表登记的文件路径算 sha256（键固定用表里的相对路径）。</summary>
    public async Task<IReadOnlyDictionary<string, string>> HashKnownFilesAsync(
        GameInstallation installation,
        IReadOnlySet<string>? excludedPaths = null,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relative in FilePaths(installation))
        {
            if (excludedPaths is not null && excludedPaths.Contains(relative))
            {
                continue;
            }

            var full = ResolveLocalPath(installation, relative);
            if (full is null)
            {
                continue;
            }

            result[relative] = await CacheStore.ComputeSha256Async(full, cancellationToken);
        }

        return result;
    }

    /// <summary>要算哈希的文件：优先用指纹表登记过的，表里没有就按本机 exe 名推导。</summary>
    private IReadOnlyList<string> FilePaths(GameInstallation installation)
    {
        if (_table.KnownFilePaths() is { Count: > 0 } known)
        {
            return known;
        }

        var exe = Path.GetFileName(installation.ExecutablePath ?? "StickFight.exe");
        var dataDir = Path.GetFileNameWithoutExtension(exe) + "_Data";

        return
        [
            exe,
            $"{dataDir}/Managed/Assembly-CSharp.dll",
            $"{dataDir}/Managed/UnityEngine.dll",
            $"{dataDir}/globalgamemanagers",
            // Unity 主场景/资源包：有 mod 会覆盖它，指纹里必须带上
            $"{dataDir}/level0",
        ];
    }

    /// <summary>把表里的相对路径映射到本机路径（数据目录名可能不同）。</summary>
    private static string? ResolveLocalPath(GameInstallation installation, string relativePath)
    {
        var direct = Path.Combine(installation.GameDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(direct))
        {
            return direct;
        }

        if (installation.ExecutablePath is null)
        {
            return null;
        }

        var localDataDir = Path.GetFileNameWithoutExtension(installation.ExecutablePath) + "_Data";
        var segments = relativePath.Split('/', 2);
        if (segments.Length != 2 || !segments[0].EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var candidate = Path.Combine(installation.GameDirectory, localDataDir, segments[1].Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(candidate) ? candidate : null;
    }
}
