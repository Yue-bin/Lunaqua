namespace Lunaqua.Domain;

/// <summary>薄策略层：不同 mod 类型的落位、凭证与启停规则（规格书 §6.1）。</summary>
public interface IModTypeStrategy
{
    /// <summary>bepinex / uvfs / uvfs-framework。</summary>
    string Type { get; }

    /// <summary>该类型文件的落位根目录。</summary>
    string ResolveInstallRoot(GameContext game, string modId);

    /// <summary>凭证文件路径。</summary>
    string ResolveCredentialPath(GameContext game, string modId);

    /// <summary>凭证是否就在安装根里（UVFS 是：启停要连凭证一起搬）。</summary>
    bool CredentialInsideInstallRoot { get; }

    /// <summary>禁用后文件整体搬到哪里。</summary>
    string ResolveDisabledRoot(GameContext game, string modId);

    /// <summary>把版本的文件清单规划成落位清单。</summary>
    InstallPlan PlanInstall(GameContext game, MetadataInfo meta, ModVersion version);

    /// <summary>该类型的额外校验；返回空表示通过。</summary>
    IReadOnlyList<string> Validate(MetadataInfo meta, ModVersion version);
}
