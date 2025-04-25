using Lunaqua.Models;
using Lunaqua.ViewModels;
using ReactiveUI;

namespace Lunaqua.ViewModels;

class PluginDetailViewModel : ViewModelBase
{
    public Plugin Plugin
    {
        get => PluginViewModel._selectedPlugin;
        set => this.RaiseAndSetIfChanged(ref PluginViewModel._selectedPlugin, value);
    }
}