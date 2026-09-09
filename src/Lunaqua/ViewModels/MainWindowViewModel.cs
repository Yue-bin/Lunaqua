using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using Lunaqua.Services;
using Serilog;

namespace Lunaqua.ViewModels;

/// <summary>外壳：抽屉导航 + 当前页面。</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IReadOnlyDictionary<string, ViewModelBase> _pages;
    private readonly IUpdateService _updates;
    private readonly ISettingsService _settings;
    private AppUpdate? _pendingUpdate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdateBanner))]
    private string? _updateBanner;

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
        TaskQueue queue,
        IUpdateService updates,
        ISettingsService settingsService)
    {
        queue.BusyChanged += (_, _) => IsBusy = queue.IsBusy;

        _updates = updates;
        _settings = settingsService;

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

    public bool HasUpdateBanner => !string.IsNullOrWhiteSpace(UpdateBanner);

    /// <summary>启动后后台检查一次（设置里可关）。</summary>
    public async Task CheckUpdateOnStartupAsync()
    {
        if (!_settings.Current.CheckUpdateOnStartup)
        {
            return;
        }

        try
        {
            var update = await _updates.CheckAsync();
            if (update is null)
            {
                return;
            }

            _pendingUpdate = update;
            UpdateBanner = $"有新版本 {update.Version}";
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "启动检查更新失败（不影响使用）");
        }
    }

    [RelayCommand]
    private async Task ApplyUpdateAsync()
    {
        if (_pendingUpdate is not { } update)
        {
            return;
        }

        try
        {
            UpdateBanner = $"正在下载 {update.Version}……";
            var progress = new Progress<int>(percent => UpdateBanner = $"正在下载 {update.Version}…… {percent}%");
            await _updates.DownloadAsync(update, progress);

            UpdateBanner = $"正在安装 {update.Version}，程序会重启……";
            await _updates.ApplyAndRestartAsync(update);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "应用更新失败");
            UpdateBanner = $"更新失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void DismissUpdate()
    {
        _pendingUpdate = null;
        UpdateBanner = null;
    }

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
