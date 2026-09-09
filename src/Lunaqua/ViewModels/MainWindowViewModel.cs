using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using Lunaqua.Services;

namespace Lunaqua.ViewModels;

/// <summary>外壳：抽屉导航 + 当前页面。</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IReadOnlyDictionary<string, ViewModelBase> _pages;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private string _currentTitle;

    [ObservableProperty]
    private bool _isDrawerOpen;

    [ObservableProperty]
    private bool _isBusy;

    public MainWindowViewModel(
        HomeViewModel home,
        ModLibraryViewModel modLibrary,
        WizardViewModel wizard,
        SettingsViewModel settings,
        TaskQueue queue)
    {
        queue.BusyChanged += (_, _) => IsBusy = queue.IsBusy;

        _pages = new Dictionary<string, ViewModelBase>(StringComparer.Ordinal)
        {
            ["home"] = home,
            ["mods"] = modLibrary,
            ["wizard"] = wizard,
            ["settings"] = settings,
        };

        NavItems =
        [
            new NavItem("home", "首页", Icon.Home, Select),
            new NavItem("mods", "Mod 库", Icon.Library, Select),
            new NavItem("wizard", "BepInEx 代装", Icon.Wrench, Select),
            new NavItem("settings", "设置", Icon.Settings, Select),
        ];

        _currentPage = home;
        _currentTitle = home.Title;
        NavItems[0].IsActive = true;
    }

    public string AppName => "Lunaqua";

    public string AppSubtitle => "「柴」mod 管理器";

    public IReadOnlyList<NavItem> NavItems { get; }

    [RelayCommand]
    private void ToggleDrawer() => IsDrawerOpen = !IsDrawerOpen;

    private void Select(string key)
    {
        if (!_pages.TryGetValue(key, out var page))
        {
            return;
        }

        foreach (var item in NavItems)
        {
            item.IsActive = item.Key == key;
        }

        CurrentPage = page;
        CurrentTitle = page.Title;
        IsDrawerOpen = false;

        // 页面自己决定要不要拉数据（Mod 库首次进入时读站点目录）
        if (page is IActivatablePage activatable)
        {
            _ = activatable.ActivateAsync();
        }
    }
}
