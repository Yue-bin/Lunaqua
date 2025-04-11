using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Microsoft.Extensions.DependencyInjection;
using Lunaqua.Services;
using Lunaqua.ViewModels;

namespace Lunaqua;

public class ViewModelLocator
{
    public static ViewModelLocator Instance { get; private set; } = null!;
    public IServiceProvider Provider { get; }

    public MainViewModel MainView => Provider.GetRequiredService<MainViewModel>();
    public ConfigViewModel Config => Provider.GetRequiredService<ConfigViewModel>();

    public ViewModelLocator()
    {
        Provider = ConfigureServices();

        Instance = this;
    }

    private IServiceProvider ConfigureServices()
    {
        var container = new ServiceCollection();
        container.AddSingleton<NavigationService>();

        container.AddScoped<MainViewModel>();
        container.AddScoped<ConfigViewModel>();

        return container.BuildServiceProvider();
    }
}