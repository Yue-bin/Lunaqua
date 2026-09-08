namespace Lunaqua.Domain;

/// <summary>站点目录里的一个可管理 mod。</summary>
/// <param name="Info">校验通过的元数据。</param>
/// <param name="DirectoryName">站上目录名。</param>
/// <param name="Warnings">解析期警告（含 id 不一致、缺作者等）。</param>
public sealed record CatalogEntry(MetadataInfo Info, string DirectoryName, IReadOnlyList<string> Warnings)
{
    /// <summary>目录名与 id 不一致 → 界面标黄（规格书 §4.2）。</summary>
    public bool IdMismatch => !string.Equals(Info.Id, DirectoryName, StringComparison.Ordinal);

    public bool HasWarnings => IdMismatch || Warnings.Count > 0;

    public string WarningText => IdMismatch
        ? $"目录名与 id 不一致（id = {Info.Id}）"
        : string.Join("；", Warnings);
}

/// <summary>无法收录的目录及原因。</summary>
/// <param name="DirectoryName">目录名。</param>
/// <param name="Errors">致命错误。</param>
public sealed record CatalogIssue(string DirectoryName, IReadOnlyList<string> Errors);

/// <summary>一次站点目录扫描的结果。</summary>
/// <param name="Entries">收录的 mod。</param>
/// <param name="Issues">被跳过的目录（无 info.json 的目录不计入）。</param>
/// <param name="LoadedAt">扫描时间。</param>
public sealed record ModCatalog(IReadOnlyList<CatalogEntry> Entries, IReadOnlyList<CatalogIssue> Issues, DateTimeOffset LoadedAt)
{
    public static ModCatalog Empty { get; } = new([], [], DateTimeOffset.MinValue);

    public int ModCount => Entries.Count;
}
