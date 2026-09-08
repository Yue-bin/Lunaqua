namespace Lunaqua.ViewModels;

/// <summary>BepInEx 代装向导：三步，默认折叠。</summary>
public sealed class WizardViewModel : ViewModelBase
{
    public override string Title => "BepInEx 代装";

    public IReadOnlyList<WizardStep> Steps { get; } =
    [
        new("1", "找到游戏", "自动从 Steam 库里定位「柴」，并检查目录结构与 64 位（PE 头）。"),
        new("2", "自动安装", "备份已有文件后部署 BepInEx 5.4.23.5 win_x64，逐文件校验哈希。"),
        new("3", "验证", "启动游戏，等日志里出现 BepInEx 5.4.23；失败可一键回滚。"),
    ];

    public string Hint => "M3 里程碑实现：三步全自动，不用你动手改文件。";
}

/// <summary>代装向导的一步。</summary>
/// <param name="Number">步骤序号。</param>
/// <param name="Title">步骤标题。</param>
/// <param name="Description">步骤说明。</param>
public sealed record WizardStep(string Number, string Title, string Description);
