using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;

namespace Lunaqua.ViewModels;

/// <summary>侧边栏导航项。</summary>
public sealed partial class NavItem : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    public NavItem(string key, string title, Icon icon, Action<string> onSelected)
    {
        Key = key;
        Title = title;
        Icon = icon;
        SelectCommand = new RelayCommand(() => onSelected(key));
    }

    /// <summary>页面键，与 <see cref="MainWindowViewModel"/> 的页面表对应。</summary>
    public string Key { get; }

    public string Title { get; }

    public Icon Icon { get; }

    public IRelayCommand SelectCommand { get; }
}
