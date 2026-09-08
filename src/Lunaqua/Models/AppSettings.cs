namespace Lunaqua.Models;

/// <summary>外观主题。</summary>
public enum AppTheme
{
    /// <summary>跟随系统。</summary>
    System,

    /// <summary>浅色。</summary>
    Light,

    /// <summary>深色。</summary>
    Dark,
}

/// <summary>落盘到 %LocalAppData%\Lunaqua\settings.json 的玩家设置。</summary>
public sealed class AppSettings
{
    /// <summary>设置文件结构版本，用于将来迁移。</summary>
    public int Schema { get; set; } = 1;

    /// <summary>「柴」的游戏根目录；未设置时为 null。</summary>
    public string? GameDirectory { get; set; }

    /// <summary>外观主题。</summary>
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>启动时后台检查 Lunaqua 自身更新（M5 生效）。</summary>
    public bool CheckUpdateOnStartup { get; set; } = true;
}
