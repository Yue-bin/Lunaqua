using Lunaqua.Models;

namespace Lunaqua.Services;

/// <summary>应用外观主题。</summary>
public interface IThemeService
{
    void Apply(AppTheme theme);
}
