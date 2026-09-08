namespace Lunaqua.ViewModels;

/// <summary>第一次进入页面时做一次初始化（例如拉站点目录）。</summary>
public interface IActivatablePage
{
    Task ActivateAsync();
}
