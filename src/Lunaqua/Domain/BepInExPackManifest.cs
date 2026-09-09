namespace Lunaqua.Domain;

/// <summary>BepInEx pack 里的一个文件。</summary>
/// <param name="Path">相对游戏根的路径。</param>
/// <param name="Sha256">小写十六进制 sha256。</param>
/// <param name="Size">字节数。</param>
public sealed record BepInExPackFile(string Path, string Sha256, long Size);

/// <summary>
/// 随程序发布的 BepInEx pack 清单（data/bepinex-pack.json）。
/// 站上 pack = 官方 5.4.23.5 win_x64 + 预置 BepInEx.cfg，共 23 个文件。
/// </summary>
public sealed record BepInExPackManifest(
    int Schema,
    string Version,
    string Architecture,
    string SourceZip,
    string SourceZipSha256,
    long SourceZipSize,
    IReadOnlyList<BepInExPackFile> Files)
{
    public static BepInExPackManifest Empty { get; } = new(1, "0.0.0", "x64", string.Empty, string.Empty, 0, []);

    public int FileCount => Files.Count;

    /// <summary>日志里应该出现的版本标记（用于验证部署成功）。</summary>
    public string LogMarker => $"BepInEx {Version}";
}
