using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lunaqua.Infrastructure.OpenList;
using Lunaqua.Services;
using Serilog;

namespace Lunaqua.ViewModels;

/// <summary>Mod 库：左侧列表 + 搜索 + 右侧详情。</summary>
public sealed partial class ModLibraryViewModel : ViewModelBase, IActivatablePage
{
    private readonly ModRepository _repository;
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

    public ModLibraryViewModel(ModRepository repository, ModDetailViewModel detail)
    {
        _repository = repository;
        Detail = detail;
    }

    public override string Title => "Mod 库";

    public ModDetailViewModel Detail { get; }

    public ObservableCollection<ModListItemViewModel> Items { get; } = [];

    public bool HasItems => Items.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

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
            _all.AddRange(catalog.Entries.Select(entry => new ModListItemViewModel(entry)));

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

    partial void OnSelectedItemChanged(ModListItemViewModel? value)
    {
        if (value is not null)
        {
            _ = Detail.LoadAsync(value.Entry);
        }
    }

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));

    private void ApplyFilter()
    {
        var query = SearchText.Trim().ToLowerInvariant();
        var filtered = query.Length == 0
            ? _all
            : [.. _all.Where(item => item.Matches(query))];

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

        StatusText = SearchText.Trim().Length == 0
            ? $"站上共 {_all.Count} 个 mod"
            : $"筛选出 {Items.Count} / {_all.Count} 个 mod";
    }
}
