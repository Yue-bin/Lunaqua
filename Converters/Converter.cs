using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Platform;
using Avalonia.Styling;
using Lunaqua.I18n;
using Lunaqua.Models;
using Lunaqua.Services;

namespace Lunaqua.Converters;

public class I18NConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key) return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);

        return Resources.ResourceManager.GetString(key, ConfigService.Instance!.GetAppCultureInfo());
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public static class ThemeConverter
{
    public static ThemeVariant PlatformThemeVar2AppThemeVar(PlatformThemeVariant systemTheme)
    {
        return systemTheme switch
        {
            PlatformThemeVariant.Light => ThemeVariant.Light,
            PlatformThemeVariant.Dark => ThemeVariant.Dark,
            _ => throw new ArgumentOutOfRangeException(nameof(systemTheme), systemTheme, null)
        };
    }

    public static ThemeVariant ThemeMode2AppThemeVar(ThemeModeEnum themeModeEnum)
    {
        return themeModeEnum switch
        {
            ThemeModeEnum.Light => ThemeVariant.Light,
            ThemeModeEnum.Dark => ThemeVariant.Dark,
            ThemeModeEnum.SyncWithSystem => PlatformThemeVar2AppThemeVar(Application.Current!.PlatformSettings!
                .GetColorValues().ThemeVariant),
            _ => throw new ArgumentOutOfRangeException(nameof(themeModeEnum), themeModeEnum, null)
        };
    }
}

public static class LanguageConverter
{
    public static string SupportedLanguage2LanguageCode(SupportedLanguageEnum languageEnum)
    {
        return languageEnum switch
        {
            SupportedLanguageEnum.English => "en-US",
            SupportedLanguageEnum.Chinese => "zh-CN",
            _ => throw new ArgumentOutOfRangeException(nameof(languageEnum), languageEnum, null)
        };
    }

    public static SupportedLanguageEnum LanguageCode2SupportedLanguage(string languageCode)
    {
        return languageCode switch
        {
            "en-US" or "en" => SupportedLanguageEnum.English,
            "zh-CN" => SupportedLanguageEnum.Chinese,
            _ => throw new ArgumentOutOfRangeException(nameof(languageCode), languageCode, null)
        };
    }
}

public class MathConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double input && parameter is string expression)
        {
            try
            {
                // 支持动态表达式，例如 "0.8" 或 "Width*0.7"
                return input * System.Convert.ToDouble(expression);
            }
            catch
            {
                return 0; // 返回安全值
            }
        }
        return 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}