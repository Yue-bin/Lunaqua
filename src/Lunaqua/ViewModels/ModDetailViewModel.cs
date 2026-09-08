namespace Lunaqua.ViewModels;

/// <summary>Mod 详情：概览 / 配置 / 版本三个 tab。</summary>
public sealed class ModDetailViewModel : ViewModelBase
{
    public override string Title => "Mod 详情";

    public string EmptyHint => "从左侧列表选一个 mod，这里显示概览、配置与版本。";

    public string OverviewHint => "概览：描述 + readme 渲染（M1）。";

    public string ConfigHint => "配置：按 .cfg 类型生成控件（M4）。";

    public string VersionHint => "版本：历史版本下拉 + 「忽略此版本」（M2）。";
}
