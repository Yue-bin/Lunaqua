using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Lunaqua.ViewModels;
using Lunaqua.Views;
using Lunaqua.Services;
using Splat;
using I18N.Avalonia;
using I18N.Avalonia.Interface;

namespace Lunaqua;

public partial class App : Application
{
    public override void Initialize()
    {
        ConfigService.LoadConfig();
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new MainWindow();
                break;
            case ISingleViewApplicationLifetime singleViewPlatform:
                singleViewPlatform.MainView = new MainView();
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }

    public override void RegisterServices()
    {
        base.RegisterServices();
        Locator.CurrentMutable.RegisterLazySingleton(() => new Localizer(I18n.Resources.ResourceManager),
            typeof(ILocalizer));
    }
}