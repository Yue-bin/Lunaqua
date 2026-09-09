namespace Lunaqua.Domain.Strategies;

/// <summary>BepInEx 插件：文件落 <c>BepInEx/plugins/&lt;path&gt;</c>，按 dll GUID 识别。</summary>
public sealed class BepInExModStrategy : IModTypeStrategy
{
    public string Type => ModTypes.BepInEx;

    public string ResolveInstallRoot(GameContext game, string modId) => game.PluginsDirectory;

    public string ResolveCredentialPath(GameContext game, string modId) =>
        Path.Combine(game.LunaquaDirectory, modId, "info.json");

    public bool CredentialInsideInstallRoot => false;

    public string ResolveDisabledRoot(GameContext game, string modId) =>
        Path.Combine(game.DisabledDirectory, modId);

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

        if (!meta.HasGuid)
        {
            errors.Add("BepInEx 插件必须带 guid。");
        }

        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in version.Files)
        {
            if (!MetadataParser.IsSafeRelativePath(file.Path))
            {
                errors.Add($"文件路径不安全：{file.Path}");
                continue;
            }

            if (!destinations.Add(file.Path))
            {
                errors.Add($"文件路径重复：{file.Path}");
            }
        }

        return errors;
    }
}
