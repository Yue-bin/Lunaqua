using System.Collections.Generic;
using Avalonia;
using Lunaqua.Converters;
using Lunaqua.Models;
using Lunaqua.Services;

namespace Lunaqua.ViewModels;

public class ConfigViewModel : ViewModelBase
{
    public static List<ThemeModeEnum> Themes =>
    [
        ThemeModeEnum.Light,
        ThemeModeEnum.Dark,
        ThemeModeEnum.SyncWithSystem
    ];

    public static List<SupportedLanguageEnum> Languages =>
    [
        SupportedLanguageEnum.English,
        SupportedLanguageEnum.Chinese
    ];

    public static ThemeModeEnum CurrentAppTheme
    {
        get => ConfigService.Instance!.AppTheme;
        set
        {
            if (value == ThemeModeEnum.SyncWithSystem)
                Application.Current!.PlatformSettings!.ColorValuesChanged +=
                    ThemeViewModel.Instance!.SystemThemeChangedEvent;
            else
                Application.Current!.PlatformSettings!.ColorValuesChanged -=
                    ThemeViewModel.Instance!.SystemThemeChangedEvent;

            ConfigService.Instance!.AppTheme = value;
            ThemeViewModel.Instance.CurrentTheme = ThemeConverter.ThemeMode2AppThemeVar(value);
            ConfigService.Instance.Save();
        }
    }
    public static SupportedLanguageEnum CurrentLanguageEnum
    {
        get => LanguageConverter.LanguageCode2SupportedLanguage(ConfigService.AppLanguage);
        set
        {
            ConfigService.AppLanguage = LanguageConverter.SupportedLanguage2LanguageCode(value);
            ConfigService.Instance!.Save();
        }
    }
}