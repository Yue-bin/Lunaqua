namespace Lunaqua.Domain;

/// <summary>一个文件从哪来、落到哪、期望哈希。</summary>
/// <param name="RemotePath">站上相对 mod 目录的路径（用于下载）。</param>
/// <param name="DestinationPath">本地绝对目标路径。</param>
/// <param name="Sha256">期望 sha256。</param>
/// <param name="Size">期望字节数。</param>
public sealed record InstallTarget(string RemotePath, string DestinationPath, string Sha256, long Size);

/// <summary>一次安装的完整计划。</summary>
/// <param name="ModId">mod id。</param>
/// <param name="Type">mod 类型。</param>
/// <param name="Version">版本号。</param>
/// <param name="Targets">落位清单。</param>
/// <param name="CredentialPath">凭证写入路径。</param>
public sealed record InstallPlan(
    string ModId,
    string Type,
    SemVer Version,
    IReadOnlyList<InstallTarget> Targets,
    string CredentialPath);
