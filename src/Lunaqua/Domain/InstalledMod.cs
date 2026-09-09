namespace Lunaqua.Domain;

/// <summary>mod 来源。</summary>
public enum ModSource
{
    /// <summary>从官方站安装。</summary>
    Official,

    /// <summary>玩家手动装、管理器纳管的。</summary>
    ManualAdopted,

    /// <summary>版本与站上任何版本都对不上（外部修改）。</summary>
    UnknownVersion,
}

/// <summary>凭证里记录的一个落位文件。</summary>
/// <param name="Path">相对安装根（或相对游戏根，由类型策略决定）的路径。</param>
/// <param name="Sha256">小写十六进制 sha256。</param>
/// <param name="Size">字节数。</param>
public sealed record InstalledFile(string Path, string Sha256, long Size);

/// <summary>本地凭证（规格书 §7.1）：识别、启停、卸载都读它。</summary>
public sealed record InstalledMod(
    int Schema,
    string Id,
    string Type,
    string? Guid,
    string Version,
    ModSource Source,
    bool Enabled,
    IReadOnlyList<InstalledFile> Files,
    DateTimeOffset InstalledAt,
    string? IgnoredVersion = null)
{
    public const int CurrentSchema = 1;

    public SemVer? ParsedVersion => SemVer.TryParse(Version, out var version) ? version : null;

    public bool IsUnknownVersion => Source == ModSource.UnknownVersion;

    public long TotalSize => Files.Sum(file => file.Size);
}
