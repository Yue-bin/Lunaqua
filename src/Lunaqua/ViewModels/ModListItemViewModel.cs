using CommunityToolkit.Mvvm.ComponentModel;
using Lunaqua.Domain;

namespace Lunaqua.ViewModels;

/// <summary>Mod 库列表里的一项。</summary>
public sealed partial class ModListItemViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    [NotifyPropertyChangedFor(nameof(IsInstalled))]
    private ModInstallationState _state;

    public ModListItemViewModel(CatalogEntry entry, ModInstallationState state)
    {
        Entry = entry;
        _state = state;
        SearchHaystack = string.Join(
            '\n',
            entry.Info.Name,
            entry.Info.Author,
            entry.Info.Id,
            entry.Info.Description,
            entry.Info.TypeDisplayName).ToLowerInvariant();
    }

    public CatalogEntry Entry { get; }

    public MetadataInfo Info => Entry.Info;

    public string Name => Info.Name;

    public string Author => Info.AuthorDisplayName;

    public string Id => Info.Id;

    public string LatestText => Info.Latest.ToString();

    public string TypeBadge => Info.TypeDisplayName;

    public string Subtitle => $"{Author} · v{LatestText} · {TypeBadge}";

    public string StatusText => State.StatusText;

    public bool HasUpdate => State.HasUpdate;

    public bool IsInstalled => State.IsInstalled;

    public bool HasWarning => Entry.HasWarnings;

    public string WarningText => Entry.WarningText;

    /// <summary>小写化的搜索文本（名字 / 作者 / id / 简介 / 类型）。</summary>
    public string SearchHaystack { get; }

    public bool Matches(string loweredQuery) => SearchHaystack.Contains(loweredQuery, StringComparison.Ordinal);
}
