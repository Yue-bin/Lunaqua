namespace Lunaqua.Domain;

/// <summary>info.json 的解析结果。</summary>
/// <param name="Info">校验通过时的元数据；有错误时为 null。</param>
/// <param name="Errors">致命错误（该 mod 不收录）。</param>
/// <param name="Warnings">可容忍的问题（界面提示，仍然收录）。</param>
public sealed record MetadataParseResult(MetadataInfo? Info, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool IsValid => Info is not null && Errors.Count == 0;
}
