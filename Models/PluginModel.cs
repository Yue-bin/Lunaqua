namespace Lunaqua.Models;

public class PluginModel
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Author { get; set; }
    public string Version { get; set; }
    public string Path { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsInstalled { get; set; }
    public bool IsUpdateAvailable { get; set; }
}