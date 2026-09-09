namespace Lunaqua.Domain.Strategies;

/// <summary>
/// UVFS 覆盖层资源 mod：文件落 <c>overlay/mods/&lt;id&gt;/&lt;path&gt;</c>，
/// 只允许 <c>data/</c> 与 <c>bundles/</c> 作用域；启停靠整个目录改名（UVFS 会枚举 mods 下全部目录）。
/// </summary>
public sealed class UvfsModStrategy : IModTypeStrategy
{
    private static readonly string[] AllowedScopes = ["data/", "bundles/"];

    public string Type => ModTypes.Uvfs;

    public string ResolveInstallRoot(GameContext game, string modId) =>
        Path.Combine(game.OverlayModsDirectory, modId);

    public string ResolveCredentialPath(GameContext game, string modId) =>
        Path.Combine(ResolveInstallRoot(game, modId), "info.json");

    public bool CredentialInsideInstallRoot => true;

    public string ResolveDisabledRoot(GameContext game, string modId) =>
        Path.Combine(game.OverlayDisabledDirectory, modId);

    public InstallPlan PlanInstall(GameContext game, MetadataInfo meta, ModVersion version)
    {
        var root = ResolveInstallRoot(game, meta.Id);
        var targets = version.Files
            .Select(file => new InstallTarget(
                file.Path,
                Path.Combine(root, file.Path.Replace('/', Path.DirectorySeparatorChar)),
                file.Sha256,
                file.Size))
            .ToList();

        return new InstallPlan(meta.Id, meta.Type, version.Version, targets, ResolveCredentialPath(game, meta.Id));
    }

    public IReadOnlyList<string> Validate(MetadataInfo meta, ModVersion version)
    {
        var errors = new List<string>();

        foreach (var file in version.Files)
        {
            if (!MetadataParser.IsSafeRelativePath(file.Path))
            {
                errors.Add($"文件路径不安全：{file.Path}");
                continue;
            }

            if (!AllowedScopes.Any(scope => file.Path.StartsWith(scope, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"UVFS mod 的文件只能放在 data/ 或 bundles/ 下：{file.Path}");
            }
        }

        return errors;
    }
}
