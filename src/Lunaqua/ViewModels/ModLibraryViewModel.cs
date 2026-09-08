using CommunityToolkit.Mvvm.ComponentModel;

namespace Lunaqua.ViewModels;

/// <summary>Mod 库：左侧列表 + 右侧详情（页内分栏）。</summary>
public sealed partial class ModLibraryViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _searchText = string.Empty;

    public ModLibraryViewModel(ModDetailViewModel detail) => Detail = detail;

    public override string Title => "Mod 库";

    public ModDetailViewModel Detail { get; }

    public string EmptyHint => "M1 里程碑接入站点后，这里会列出站上的 mod。";

    public string ListHint => "左侧列表会在 M1 里程碑接上站点目录。";
}
