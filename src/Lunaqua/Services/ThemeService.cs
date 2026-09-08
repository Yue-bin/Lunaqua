using Avalonia;
using Avalonia.Styling;
using Lunaqua.Models;

namespace Lunaqua.Services;

/// <summary>把 <see cref="AppTheme"/> 映射到 Avalonia 的 <see cref="ThemeVariant"/>。</summary>
public sealed class ThemeService : IThemeService
{
    public void Apply(AppTheme theme)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        app.RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
