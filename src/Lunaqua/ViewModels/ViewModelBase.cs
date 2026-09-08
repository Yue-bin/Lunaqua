using CommunityToolkit.Mvvm.ComponentModel;

namespace Lunaqua.ViewModels;

/// <summary>所有页面 ViewModel 的基类。</summary>
public abstract class ViewModelBase : ObservableObject
{
    /// <summary>页面标题（外壳顶栏显示）。</summary>
    public abstract string Title { get; }
}
