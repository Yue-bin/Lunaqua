namespace Lunaqua.Domain;

/// <summary>「柴」的游戏根目录与相关落位路径（规格书 §6.1、§7.1）。</summary>
/// <param name="GameDirectory">游戏根目录。</param>
public sealed record GameContext(string GameDirectory)
{
    /// <summary>BepInEx 插件目录。</summary>
    public string PluginsDirectory => Path.Combine(GameDirectory, "BepInEx", "plugins");

    /// <summary>管理器自己的记账目录。</summary>
    public string LunaquaDirectory => Path.Combine(GameDirectory, ".lunaqua");

    /// <summary>BepInEx 类 mod 的「已禁用」目录。</summary>
    public string DisabledDirectory => Path.Combine(GameDirectory, ".lunaqua-disabled");

    /// <summary>安装备份根目录。</summary>
    public string BackupDirectory => Path.Combine(LunaquaDirectory, "backup");

    /// <summary>UVFS 覆盖层 mod 目录。</summary>
    public string OverlayModsDirectory => Path.Combine(GameDirectory, "overlay", "mods");

    /// <summary>UVFS 覆盖层 mod 的「已禁用」目录。</summary>
    public string OverlayDisabledDirectory => Path.Combine(GameDirectory, "overlay", "mods.disabled");

    public string Combine(string relativePath) =>
        Path.Combine(GameDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
