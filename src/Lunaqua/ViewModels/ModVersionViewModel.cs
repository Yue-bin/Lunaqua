using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Lunaqua.Domain;

namespace Lunaqua.ViewModels;

/// <summary>详情页「版本」tab 的一项。</summary>
public sealed partial class ModVersionViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    private bool _isInstalled;

    public ModVersionViewModel(ModVersion version, bool isLatest, bool isInstalled = false)
    {
        Version = version;
        IsLatest = isLatest;
        _isInstalled = isInstalled;
    }

    public ModVersion Version { get; }

    public bool IsLatest { get; }

    public string VersionText => $"v{Version.Version}";

    public string BadgeText => IsInstalled ? "已装" : IsLatest ? "最新" : string.Empty;

    public bool HasBadge => BadgeText.Length > 0;

    public string ReleasedText => string.IsNullOrWhiteSpace(Version.Released) ? "未标注日期" : Version.Released;

    public string FileSummary
    {
        get
        {
            var total = Version.Files.Sum(file => file.Size);
            return $"{Version.Files.Count} 个文件 · {FormatSize(total)}";
        }
    }

    public bool HasChangelog => !string.IsNullOrWhiteSpace(Version.Changelog);

    public string ChangelogText => Version.Changelog ?? string.Empty;

    public string FileListText => string.Join('\n', Version.Files.Select(file => $"{file.Path}（{FormatSize(file.Size)}）"));

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} B"
            : string.Create(CultureInfo.InvariantCulture, $"{value:0.#} {units[unit]}");
    }
}
