namespace Lunaqua.Domain;

/// <summary>指纹表里的一个文件。</summary>
/// <param name="Path">相对游戏根的路径。</param>
/// <param name="Sha256">小写十六进制 sha256。</param>
/// <param name="Size">字节数。</param>
public sealed record FingerprintFile(string Path, string Sha256, long Size);

/// <summary>一个官方构建的指纹。</summary>
/// <param name="BuildId">Steam buildid（第一层判定的主键）。</param>
/// <param name="Depot">depot id。</param>
/// <param name="Manifest">manifest id。</param>
/// <param name="Exe">exe 名。</param>
/// <param name="DataDir">数据目录名。</param>
/// <param name="Files">关键文件哈希；空表示只登记了 buildid。</param>
public sealed record GameFingerprint(
    string BuildId,
    string Depot,
    string Manifest,
    string Exe,
    string DataDir,
    IReadOnlyList<FingerprintFile> Files)
{
    public bool HasHashes => Files.Count > 0;

    public bool MatchesBuildId(string? buildId) =>
        !string.IsNullOrWhiteSpace(buildId) && string.Equals(BuildId, buildId.Trim(), StringComparison.Ordinal);
}

/// <summary>官方构建指纹表（data/game-fingerprints.json）。</summary>
public sealed record GameFingerprintTable(int Schema, string AppId, IReadOnlyList<GameFingerprint> Builds)
{
    public static GameFingerprintTable Empty { get; } = new(1, "674940", []);

    public int Count => Builds.Count;

    public GameFingerprint? FindByBuildId(string? buildId) =>
        Builds.FirstOrDefault(build => build.MatchesBuildId(buildId));

    /// <summary>本机哈希是否与表里某个构建完全一致（表里登记的文件必须全部命中）。</summary>
    public GameFingerprint? MatchByHashes(IReadOnlyDictionary<string, string> localHashes)
    {
        foreach (var build in Builds)
        {
            if (!build.HasHashes)
            {
                continue;
            }

            var allMatch = build.Files.All(file =>
                localHashes.TryGetValue(file.Path, out var hash)
                && string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase));

            if (allMatch)
            {
                return build;
            }
        }

        return null;
    }

    /// <summary>表里登记过的全部文件路径（用于决定要算哪些哈希）。</summary>
    public IReadOnlyList<string> KnownFilePaths() =>
        [.. Builds.SelectMany(build => build.Files).Select(file => file.Path).Distinct(StringComparer.OrdinalIgnoreCase)];
}
