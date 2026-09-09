using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lunaqua.Domain;
using Lunaqua.Infrastructure.OpenList;
using Lunaqua.Services;
using Serilog;

namespace Lunaqua.ViewModels;

/// <summary>Mod 库：左侧列表 + 搜索 + 右侧详情。</summary>
public sealed partial class ModLibraryViewModel : ViewModelBase, IActivatablePage
{
    private readonly ModRepository _repository;
    private readonly InstalledModService _installed;
    private readonly ISettingsService _settings;
    private readonly List<ModListItemViewModel> _all = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusText = "还没读取站点目录。";

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private ModListItemViewModel? _selectedItem;

    [ObservableProperty]
    private bool _onlyUpdatable;

    public ModLibraryViewModel(
        ModRepository repository,
        InstalledModService installed,
        ISettingsService settings,
        ModDetailViewModel detail)
    {
        _repository = repository;
        _installed = installed;
        _settings = settings;
        Detail = detail;
        Detail.StateChanged += (_, _) => RefreshStates();
        _settings.Changed += (_, _) => RefreshStates();
    }

    public override string Title => "Mod 库";

    public ModDetailViewModel Detail { get; }

    public ObservableCollection<ModListItemViewModel> Items { get; } = [];

    public bool HasItems => Items.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public string EmptyHint => "站上还没有可管理的 mod，或者还没读取目录。";

    /// <summary>第一次进入页面时自动拉一次目录。</summary>
    public Task ActivateAsync() => _all.Count > 0 || IsLoading ? Task.CompletedTask : RefreshAsync();

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        IsLoading = true;
        ErrorText = null;
        StatusText = "正在读取站点目录…";

        try
        {
            var catalog = await _repository.LoadCatalogAsync();

            _all.Clear();
            foreach (var entry in catalog.Entries)
            {
                _all.Add(new ModListItemViewModel(entry, GetState(entry)));
            }

            ApplyFilter();

            if (catalog.Issues.Count > 0)
            {
                ErrorText = string.Join("\n", catalog.Issues.Select(issue => $"{issue.DirectoryName}：{string.Join("，", issue.Errors)}"));
            }
        }
        catch (Exception ex) when (ex is OpenListException or HttpRequestException or TaskCanceledException)
        {
            Log.Warning(ex, "读取站点目录失败");
            ErrorText = $"读取站点目录失败：{ex.Message}";
            StatusText = "读取失败，点「刷新」再试一次。";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanRefresh() => !IsLoading;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnOnlyUpdatableChanged(bool value) => ApplyFilter();

    partial void OnSelectedItemChanged(ModListItemViewModel? value)
    {
        if (value is not null)
        {
            _ = Detail.LoadAsync(value.Entry);
        }
    }

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    /// <summary>安装/启停之后重算每个条目的状态。</summary>
    private void RefreshStates()
    {
        foreach (var item in _all)
        {
            item.State = GetState(item.Entry);
        }

        if (OnlyUpdatable)
        {
            ApplyFilter();
        }
    }

    private ModInstallationState GetState(CatalogEntry entry) =>
        GameContext is { } game
            ? _installed.GetState(game, entry)
            : new ModInstallationState(null, entry.Info.Latest, null);

    private GameContext? GameContext => _settings.Current.GameDirectory is { Length: > 0 } directory
        ? new GameContext(directory)
        : null;

    private void ApplyFilter()
    {
        var query = SearchText.Trim().ToLowerInvariant();

        IEnumerable<ModListItemViewModel> filtered = _all;
        if (query.Length > 0)
        {
            filtered = filtered.Where(item => item.Matches(query));
        }

        if (OnlyUpdatable)
        {
            filtered = filtered.Where(item => item.HasUpdate);
        }

        Items.Clear();
        foreach (var item in filtered)
        {
            Items.Add(item);
        }

        OnPropertyChanged(nameof(HasItems));
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (HasError)
        {
            return;
        }

        if (_all.Count == 0)
        {
            StatusText = IsLoading ? "正在读取站点目录…" : "还没读取站点目录。";
            return;
        }

        var filtered = SearchText.Trim().Length > 0 || OnlyUpdatable;
        StatusText = filtered
            ? $"筛选出 {Items.Count} / {_all.Count} 个 mod"
            : $"站上共 {_all.Count} 个 mod";
    }
}
