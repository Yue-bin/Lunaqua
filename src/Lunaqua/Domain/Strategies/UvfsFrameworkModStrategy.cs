namespace Lunaqua.Domain.Strategies;

/// <summary>
/// UVFS 框架本体：管理器内置映射表（规格书 §6.1）。
/// 站上结构与映射不符 → 拒绝安装并提示升级管理器。
/// </summary>
public sealed class UvfsFrameworkModStrategy : IModTypeStrategy
{
    private static readonly string[] NativeLoaderFiles =
    [
        "BepInEx/patchers/Overlay.NativeLoader/overlay32.dll",
        "BepInEx/patchers/Overlay.NativeLoader/overlay64.dll",
        "BepInEx/patchers/Overlay.NativeLoader/Overlay.NativeLoader.dll",
    ];

    private const string ManagedFile = "BepInEx/plugins/Overlay.Managed.dll";

    private const string ResourceRedirectorScope = "BepInEx/plugins/XUnity.ResourceRedirector/";

    public string Type => ModTypes.UvfsFramework;

    public string ResolveInstallRoot(GameContext game, string modId) => game.GameDirectory;

    public string ResolveCredentialPath(GameContext game, string modId) =>
        Path.Combine(game.LunaquaDirectory, modId, "info.json");

    public bool CredentialInsideInstallRoot => false;

    public string ResolveDisabledRoot(GameContext game, string modId) =>
        Path.Combine(game.DisabledDirectory, modId);

    public InstallPlan PlanInstall(GameContext game, MetadataInfo meta, ModVersion version)
    {
        var targets = version.Files
            .Select(file => new InstallTarget(
                file.Path,
                game.Combine(file.Path),
                file.Sha256,
                file.Size))
            .ToList();

        return new InstallPlan(meta.Id, meta.Type, version.Version, targets, ResolveCredentialPath(game, meta.Id));
    }

    public IReadOnlyList<string> Validate(MetadataInfo meta, ModVersion version)
    {
        var errors = new List<string>();

        if (!meta.HasGuid)
        {
            errors.Add("UVFS 框架必须带 guid。");
        }

        foreach (var file in version.Files)
        {
            if (!MetadataParser.IsSafeRelativePath(file.Path))
            {
                errors.Add($"文件路径不安全：{file.Path}");
                continue;
            }

            if (!IsMapped(file.Path))
            {
                errors.Add($"站上结构与内置映射表不符（{file.Path}），请升级 Lunaqua。");
            }
        }

        return errors;
    }

    /// <summary>判断站上路径是否命中内置映射表。</summary>
    public static bool IsMapped(string path) =>
        NativeLoaderFiles.Contains(path, StringComparer.OrdinalIgnoreCase)
        || string.Equals(path, ManagedFile, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(ResourceRedirectorScope, StringComparison.OrdinalIgnoreCase);
}
