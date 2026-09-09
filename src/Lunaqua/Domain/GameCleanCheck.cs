namespace Lunaqua.Domain;

/// <summary>干净判定的结论。</summary>
public enum GameCleanStatus
{
    /// <summary>没法判定（没装游戏 / 指纹表为空）。</summary>
    Unknown,

    /// <summary>buildid 或文件哈希命中官方指纹表。</summary>
    Clean,

    /// <summary>命中玩家自己信任过的本机基线（第三层兜底）。</summary>
    Trusted,

    /// <summary>不认识 → 引导 Steam 校验后复扫。</summary>
    NeedsValidate,
}

/// <summary>一次干净判定的结果。</summary>
/// <param name="Status">结论。</param>
/// <param name="Message">给玩家看的中文说明。</param>
/// <param name="BuildId">本机 appmanifest 的 buildid。</param>
/// <param name="MatchedBuildId">命中的官方构建（可能为空）。</param>
/// <param name="MismatchedFiles">对不上的文件。</param>
public sealed record GameCleanCheckResult(
    GameCleanStatus Status,
    string Message,
    string? BuildId,
    string? MatchedBuildId,
    IReadOnlyList<string> MismatchedFiles)
{
    public bool IsClean => Status is GameCleanStatus.Clean or GameCleanStatus.Trusted;

    public static GameCleanCheckResult Unknown(string message) => new(GameCleanStatus.Unknown, message, null, null, []);
}

/// <summary>本机首扫基线（干净判定第三层）。</summary>
/// <param name="Schema">结构版本。</param>
/// <param name="BuildId">采集时的 buildid。</param>
/// <param name="Exe">exe 名。</param>
/// <param name="CapturedAt">采集时间。</param>
/// <param name="FileHashes">相对路径 → sha256。</param>
public sealed record TrustedBaseline(
    int Schema,
    string? BuildId,
    string Exe,
    DateTimeOffset CapturedAt,
    IReadOnlyDictionary<string, string> FileHashes)
{
    public const int CurrentSchema = 1;

    /// <summary>本机哈希是否与基线一致。</summary>
    public bool Matches(IReadOnlyDictionary<string, string> localHashes) =>
        FileHashes.Count > 0
        && FileHashes.All(pair =>
            localHashes.TryGetValue(pair.Key, out var hash)
            && string.Equals(hash, pair.Value, StringComparison.OrdinalIgnoreCase));
}
