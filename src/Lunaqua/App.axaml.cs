using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lunaqua.Services;
using Lunaqua.ViewModels;
using Lunaqua.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Lunaqua;

public partial class App : Application
{
    private ServiceProvider? _services;

    /// <summary>DI 容器（无头测试与后续里程碑使用）。</summary>
    public IServiceProvider Services => _services
        ?? throw new InvalidOperationException("应用尚未初始化。");

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var settings = new SettingsService();
        settings.Load();

        var services = new ServiceCollection();
        services.AddSingleton<ISettingsService>(settings);
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<ModDetailViewModel>();
        services.AddSingleton<ModLibraryViewModel>();
        services.AddSingleton<WizardViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        _services = services.BuildServiceProvider();

        var theme = _services.GetRequiredService<IThemeService>();
        theme.Apply(settings.Current.Theme);
        settings.Changed += (_, _) => theme.Apply(settings.Current.Theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>(),
            };
            desktop.Exit += (_, _) => Shutdown();
        }

        Log.Information("界面已就绪");
        base.OnFrameworkInitializationCompleted();
    }

    private void Shutdown()
    {
        _services?.Dispose();
        _services = null;
        Log.CloseAndFlush();
    }
}
