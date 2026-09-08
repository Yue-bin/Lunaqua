namespace Lunaqua.Domain;

/// <summary>info.json 里的一个文件条目。</summary>
/// <param name="Path">相对 mod 目录的路径。</param>
/// <param name="Sha256">小写十六进制 sha256。</param>
/// <param name="Size">字节数。</param>
public sealed record MetadataFile(string Path, string Sha256, long Size);

/// <summary>依赖项。</summary>
/// <param name="Id">依赖的 mod id。</param>
/// <param name="MinVersion">最低版本（可空）。</param>
public sealed record MetadataDependency(string Id, SemVer? MinVersion);

/// <summary>一个发布版本。</summary>
/// <param name="Version">SemVer 版本号。</param>
/// <param name="Released">发布日期（ISO 8601）。</param>
/// <param name="Files">该版本的文件清单。</param>
/// <param name="Changelog">更新说明（可空）。</param>
public sealed record ModVersion(SemVer Version, string Released, IReadOnlyList<MetadataFile> Files, string? Changelog);

/// <summary>站上某个 mod 目录的 info.json（规格书 §4.2）。</summary>
public sealed record MetadataInfo(
    int Schema,
    string Id,
    string Type,
    string? Guid,
    string Name,
    string Author,
    string Description,
    SemVer Latest,
    IReadOnlyList<MetadataDependency> Dependencies,
    IReadOnlyList<ModVersion> Versions)
{
    /// <summary>bepinex / uvfs-framework 必须带 guid。</summary>
    public bool HasGuid => !string.IsNullOrWhiteSpace(Guid);

    public string TypeDisplayName => ModTypes.DisplayName(Type);

    public string AuthorDisplayName => string.IsNullOrWhiteSpace(Author) ? "未知作者" : Author;

    public ModVersion? FindVersion(SemVer version) => Versions.FirstOrDefault(v => v.Version.Equals(version));

    /// <summary>latest 对应的版本；latest 校验通过时必然存在。</summary>
    public ModVersion LatestVersion => FindVersion(Latest) ?? Versions[0];

    /// <summary>按版本号从新到旧排序的版本列表。</summary>
    public IReadOnlyList<ModVersion> VersionsDescending =>
        [.. Versions.OrderByDescending(v => v.Version)];
}
