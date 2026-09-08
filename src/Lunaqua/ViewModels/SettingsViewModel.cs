using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lunaqua.Models;
using Lunaqua.Services;

namespace Lunaqua.ViewModels;

/// <summary>设置页：游戏目录 / 外观 / 更新 / 数据目录 / 关于。</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly bool _initialized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameDirectoryText))]
    [NotifyPropertyChangedFor(nameof(HasGameDirectory))]
    [NotifyPropertyChangedFor(nameof(GameDirectoryHint))]
    private string? _gameDirectory;

    [ObservableProperty]
    private ThemeOption _selectedTheme;

    [ObservableProperty]
    private bool _checkUpdateOnStartup;

    public SettingsViewModel(ISettingsService settings, IDialogService dialogs)
    {
        _settings = settings;
        _dialogs = dialogs;

        ThemeOptions =
        [
            new ThemeOption(AppTheme.System, "跟随系统"),
            new ThemeOption(AppTheme.Light, "浅色"),
            new ThemeOption(AppTheme.Dark, "深色"),
        ];

        GameDirectory = settings.Current.GameDirectory;
        _selectedTheme = ThemeOptions.FirstOrDefault(o => o.Value == settings.Current.Theme) ?? ThemeOptions[0];
        _checkUpdateOnStartup = settings.Current.CheckUpdateOnStartup;
        _initialized = true;
    }

    public override string Title => "设置";

    public IReadOnlyList<ThemeOption> ThemeOptions { get; }

    public string VersionText => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public string DataDirectory => AppPaths.Root;

    public string CacheDirectory => AppPaths.Cache;

    public string GameDirectoryText => GameDirectory is { Length: > 0 } game ? game : "未设置";

    public bool HasGameDirectory => GameDirectory is { Length: > 0 };

    public string GameDirectoryHint => GameDirectory is { Length: > 0 } game
        ? DescribeGameDirectory(game)
        : "还没选游戏目录。点「选择目录」挑一个就行。";

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        if (!_initialized || value is null)
        {
            return;
        }

        _settings.Update(s => s.Theme = value.Value);
    }

    partial void OnCheckUpdateOnStartupChanged(bool value)
    {
        if (!_initialized)
        {
            return;
        }

        _settings.Update(s => s.CheckUpdateOnStartup = value);
    }

    [RelayCommand]
    private async Task PickGameDirectoryAsync()
    {
        var folder = await _dialogs.PickFolderAsync("选择「柴」的游戏目录", GameDirectory);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        GameDirectory = folder;
        _settings.Update(s => s.GameDirectory = folder);
    }

    [RelayCommand]
    private void ClearGameDirectory()
    {
        GameDirectory = null;
        _settings.Update(s => s.GameDirectory = null);
    }

    [RelayCommand]
    private void OpenDataDirectory() => OpenDirectory(AppPaths.Root);

    [RelayCommand]
    private void OpenCacheDirectory() => OpenDirectory(AppPaths.Cache);

    private static void OpenDirectory(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static string DescribeGameDirectory(string path)
    {
        foreach (var exe in new[] { "StickFight.exe", "StickFightTheGame.exe" })
        {
            if (File.Exists(Path.Combine(path, exe)))
            {
                return $"已找到 {exe}。";
            }
        }

        return "这个目录里没找到 StickFight.exe —— 先选上，M3 代装时还会再校验一次。";
    }
}

/// <summary>外观下拉项。</summary>
/// <param name="Value">主题值。</param>
/// <param name="Label">中文名。</param>
public sealed record ThemeOption(AppTheme Value, string Label);
