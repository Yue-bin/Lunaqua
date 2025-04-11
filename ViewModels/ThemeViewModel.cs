using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Lunaqua.Models;
using Lunaqua.Services;
using Lunaqua.Converters;
using ReactiveUI;

namespace Lunaqua.ViewModels;

public class ThemeViewModel : ViewModelBase
{
    public static ThemeViewModel? Instance;

    private static readonly Dictionary<ThemeVariant, Color> ThemeColors = new()
    {
        { ThemeVariant.Light, Colors.SeaShell },
        { ThemeVariant.Dark, Color.FromArgb(51, 46, 46, 20) }
    };

    private ThemeVariant _currentTheme = ThemeConverter.ThemeMode2AppThemeVar(ConfigService.Instance!.AppTheme);

    private Color _themeColor = ThemeColors[ThemeConverter.ThemeMode2AppThemeVar(ConfigService.Instance.AppTheme)];

    public ThemeVariant CurrentTheme
    {
        get => _currentTheme;
        set
        {
            BaseColor = ThemeColors[value];
            this.RaiseAndSetIfChanged(ref _currentTheme, value);
        }
    }

    public Color BaseColor
    {
        get => _themeColor;
        set => this.RaiseAndSetIfChanged(ref _themeColor, value);
    }

    protected ThemeViewModel()
    {
        if (ConfigService.Instance!.AppTheme == ThemeModeEnum.SyncWithSystem)
            Application.Current!.PlatformSettings!.ColorValuesChanged += SystemThemeChangedEvent;
        Instance = this;
    }

    public void SystemThemeChangedEvent(object? o, PlatformColorValues values)
    {
        CurrentTheme = ThemeConverter.PlatformThemeVar2AppThemeVar(values.ThemeVariant);
        BaseColor = ThemeColors[ThemeConverter.PlatformThemeVar2AppThemeVar(values.ThemeVariant)];
    }
}