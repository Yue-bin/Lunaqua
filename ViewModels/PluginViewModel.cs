using System.Collections.Generic;
using Avalonia;
using ReactiveUI;
using System.Collections.ObjectModel;
using Lunaqua.Converters;
using Lunaqua.Models;
using Lunaqua.Services;

namespace Lunaqua.ViewModels;

public class PluginViewModel : ViewModelBase
{
    private ObservableCollection<Plugin> _plugins = [];
    public void LoadPlugins()
    {
        // This method would typically load plugins from a directory or a database
        // For this example, we'll just create some sample data

        // Sample data for plugins
        _plugins = new ObservableCollection<Plugin>();
        for (var i = 0; i < 10; i++)
        {
            _plugins.Add(new Plugin
            {
                Name = $"Plugin {i + 1}",
                Description = $"Description for Plugin {i + 1}",
                Author = $"Author {i + 1}",
                Version = $"1.0.{i + 1}",
                Path = $"/path/to/plugin{i + 1}.dll",
                IsEnabled = true,
                IsInstalled = true,
                IsUpdateAvailable = false
            });
        }
        _selectedPlugin = _plugins[0]; // Select the first plugin by default
    }

    public ObservableCollection<Plugin> Plugins
    {
        get => _plugins;
        set => this.RaiseAndSetIfChanged(ref _plugins, value);
    }

    static public Plugin _selectedPlugin;

    public PluginViewModel()
    {
        // Initialize the plugins collection with some sample data
        LoadPlugins();
    }
}