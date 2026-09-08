namespace Lunaqua.Services;

/// <summary>与窗口相关的交互（目录选择等）。</summary>
public interface IDialogService
{
    /// <summary>弹出目录选择器；用户取消时返回 null。</summary>
    Task<string?> PickFolderAsync(string title, string? startAt = null);
}
