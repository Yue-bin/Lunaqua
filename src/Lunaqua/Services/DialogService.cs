using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Lunaqua.Services;

/// <summary>基于主窗口 StorageProvider 的实现。</summary>
public sealed class DialogService : IDialogService
{
    public async Task<string?> PickFolderAsync(string title, string? startAt = null)
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (window is null)
        {
            return null;
        }

        IStorageFolder? start = null;
        if (!string.IsNullOrWhiteSpace(startAt) && Directory.Exists(startAt))
        {
            start = await window.StorageProvider.TryGetFolderFromPathAsync(startAt);
        }

        var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
