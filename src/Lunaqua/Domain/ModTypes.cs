namespace Lunaqua.Domain;

/// <summary>mod 类型常量（见规格书 §3）。</summary>
public static class ModTypes
{
    /// <summary>BepInEx 插件（dll）。</summary>
    public const string BepInEx = "bepinex";

    /// <summary>UVFS 覆盖层资源 mod。</summary>
    public const string Uvfs = "uvfs";

    /// <summary>UVFS 框架本体（特例）。</summary>
    public const string UvfsFramework = "uvfs-framework";

    public static bool IsKnown(string? type) => type is BepInEx or Uvfs or UvfsFramework;

    /// <summary>界面上显示的中文类型名。</summary>
    public static string DisplayName(string? type) => type switch
    {
        Uvfs => "UVFS 资源",
        UvfsFramework => "UVFS 框架",
        BepInEx => "BepInEx 插件",
        _ => "未知类型",
    };
}
