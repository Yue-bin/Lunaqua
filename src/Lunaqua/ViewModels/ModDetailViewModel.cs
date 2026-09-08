using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Lunaqua.Domain;
using Lunaqua.Services;
using Serilog;

namespace Lunaqua.ViewModels;

/// <summary>Mod 详情：概览 / 配置 / 版本三个 tab。</summary>
public sealed partial class ModDetailViewModel : ViewModelBase
{
    private readonly ModRepository _repository;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _author = string.Empty;

    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _typeBadge = string.Empty;

    [ObservableProperty]
    private string _latestText = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _readmeText = string.Empty;

    [ObservableProperty]
    private string? _warningText;

    [ObservableProperty]
    private bool _isLoadingReadme;

    public ModDetailViewModel(ModRepository repository) => _repository = repository;

    public override string Title => "Mod 详情";

    public ObservableCollection<ModVersionViewModel> Versions { get; } = [];

    public string EmptyHint => "从左侧列表选一个 mod，这里显示概览、配置与版本。";

    public string ConfigHint => "配置：按 .cfg 类型生成控件（M4 里程碑）。";

    public string MetaText => $"id {Id} · {TypeBadge} · {Versions.Count} 个版本";

    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningText);

    /// <summary>载入一个 mod 的元数据，并异步拉 readme。</summary>
    public async Task LoadAsync(CatalogEntry entry)
    {
        Name = entry.Info.Name;
        Author = entry.Info.AuthorDisplayName;
        Id = entry.Info.Id;
        TypeBadge = entry.Info.TypeDisplayName;
        LatestText = entry.Info.Latest.ToString();
        Description = string.IsNullOrWhiteSpace(entry.Info.Description) ? "（这个 mod 没有简介）" : entry.Info.Description;
        WarningText = entry.HasWarnings ? entry.WarningText : null;
        HasSelection = true;

        Versions.Clear();
        foreach (var version in entry.Info.VersionsDescending)
        {
            Versions.Add(new ModVersionViewModel(version, version.Version.Equals(entry.Info.Latest)));
        }

        OnPropertyChanged(nameof(HasWarning));
        OnPropertyChanged(nameof(MetaText));

        ReadmeText = string.Empty;
        IsLoadingReadme = true;
        try
        {
            var readme = await _repository.ReadReadmeAsync(entry.DirectoryName);
            ReadmeText = string.IsNullOrWhiteSpace(readme) ? "（这个 mod 没有 readme.md）" : readme;
        }
        catch (Exception ex)
        {
            ReadmeText = $"readme 读取失败：{ex.Message}";
            Log.Warning(ex, "读取 {Directory} 的 readme 失败", entry.DirectoryName);
        }
        finally
        {
            IsLoadingReadme = false;
        }
    }
}
