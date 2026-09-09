using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lunaqua.Domain;
using Lunaqua.Services;
using Serilog;

namespace Lunaqua.ViewModels;

/// <summary>Mod 详情：概览 / 配置 / 版本三个 tab，以及安装 / 更新 / 启停 / 卸载动作。</summary>
public sealed partial class ModDetailViewModel : ViewModelBase
{
    private readonly ModRepository _repository;
    private readonly InstallEngine _engine;
    private readonly InstalledModService _installed;
    private readonly ISettingsService _settings;
    private readonly TaskQueue _queue;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallButtonText))]
    [NotifyPropertyChangedFor(nameof(ToggleEnabledText))]
    [NotifyPropertyChangedFor(nameof(CanToggleEnabled))]
    [NotifyPropertyChangedFor(nameof(CanIgnoreUpdate))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private ModInstallationState? _state;

    [ObservableProperty]
    private string _actionStatus = string.Empty;

    private CatalogEntry? _entry;

    public ModDetailViewModel(
        ModRepository repository,
        InstallEngine engine,
        InstalledModService installed,
        ISettingsService settings,
        TaskQueue queue,
        ConfigTabViewModel config)
    {
        _repository = repository;
        _engine = engine;
        _installed = installed;
        _settings = settings;
        _queue = queue;
        Config = config;
    }

    /// <summary>「配置」tab 的 ViewModel。</summary>
    public ConfigTabViewModel Config { get; }

    /// <summary>安装/卸载/启停之后通知列表刷新状态。</summary>
    public event EventHandler? StateChanged;

    public override string Title => "Mod 详情";

    public ObservableCollection<ModVersionViewModel> Versions { get; } = [];

    public string EmptyHint => "从左侧列表选一个 mod，这里显示概览、配置与版本。";

    public string ConfigHint => "配置：按 .cfg 类型生成控件（M4 里程碑）。";

    public string MetaText => $"id {Id} · {TypeBadge} · {Versions.Count} 个版本";

    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningText);

    public string StatusText => State?.StatusText ?? string.Empty;

    public bool HasUpdate => State?.HasUpdate ?? false;

    public string InstallButtonText => State switch
    {
        { IsInstalled: true, HasUpdate: true } => "更新到最新版",
        { IsInstalled: true } => "重新安装最新版",
        _ => "安装最新版",
    };

    public string ToggleEnabledText => State?.IsEnabled == true ? "禁用" : "启用";

    public bool CanToggleEnabled => State is { IsInstalled: true, IsUnknownVersion: false };

    public bool CanIgnoreUpdate => State?.HasUpdate == true;

    /// <summary>载入一个 mod 的元数据，并异步拉 readme。</summary>
    public async Task LoadAsync(CatalogEntry entry)
    {
        _entry = entry;
        Name = entry.Info.Name;
        Author = entry.Info.AuthorDisplayName;
        Id = entry.Info.Id;
        TypeBadge = entry.Info.TypeDisplayName;
        LatestText = entry.Info.Latest.ToString();
        Description = string.IsNullOrWhiteSpace(entry.Info.Description) ? "（这个 mod 没有简介）" : entry.Info.Description;
        WarningText = entry.HasWarnings ? entry.WarningText : null;
        HasSelection = true;
        ActionStatus = string.Empty;

        OnPropertyChanged(nameof(HasWarning));

        Versions.Clear();
        foreach (var version in entry.Info.VersionsDescending)
        {
            Versions.Add(new ModVersionViewModel(version, version.Version.Equals(entry.Info.Latest)));
        }

        RefreshState();
        Config.Load(entry.Info.Guid, entry.Info.Id);

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

    /// <summary>重新读凭证与磁盘，刷新状态与版本列表标记。</summary>
    public void RefreshState()
    {
        if (_entry is null || GameContext is not { } game)
        {
            State = null;
        }
        else
        {
            State = _installed.GetState(game, _entry);
        }

        var installedVersion = State?.InstalledVersion;
        foreach (var version in Versions)
        {
            version.IsInstalled = installedVersion is not null && version.Version.Version.Equals(installedVersion);
        }

        OnPropertyChanged(nameof(MetaText));
    }

    [RelayCommand]
    private Task InstallLatestAsync() => InstallVersionAsync(_entry?.Info.LatestVersion);

    [RelayCommand]
    private Task InstallVersionAsync(ModVersionViewModel? version) =>
        InstallVersionAsync(version?.Version ?? _entry?.Info.LatestVersion);

    [RelayCommand]
    private Task ToggleEnabledAsync() => RunAsync(
        State?.IsEnabled == true ? "禁用" : "启用",
        game =>
        {
            _engine.SetEnabled(game, State!.Installed!, State.IsEnabled == false);
            return Task.CompletedTask;
        });

    [RelayCommand]
    private Task UninstallAsync() => RunAsync(
        "卸载",
        game =>
        {
            _engine.Uninstall(game, State!.Installed!);
            return Task.CompletedTask;
        });

    [RelayCommand]
    private Task IgnoreUpdateAsync() => RunAsync(
        "忽略此版本",
        game =>
        {
            var installed = State!.Installed!;
            _engine.SaveCredential(game, installed with { IgnoredVersion = _entry!.Info.Latest.ToString() });
            return Task.CompletedTask;
        });

    private Task InstallVersionAsync(ModVersion? version)
    {
        if (_entry is not { } entry || version is null)
        {
            return Task.CompletedTask;
        }

        // 用同步回调，避免进度通知晚于「完成」覆盖状态行
        var progress = new InlineProgress<InstallProgress>(report =>
            ActionStatus = $"{report.Stage} {report.Done}/{report.Total}");

        return RunAsync(
            $"安装 v{version.Version}",
            game => _engine.InstallAsync(game, entry.DirectoryName, entry.Info, version, progress));
    }

    private async Task RunAsync(string what, Func<GameContext, Task> action)
    {
        if (GameContext is not { } game)
        {
            ActionStatus = "先去「设置」里选游戏目录。";
            return;
        }

        ActionStatus = $"{what}中…";
        try
        {
            await _queue.RunAsync(() => action(game));
            ActionStatus = $"{what}完成。";
        }
        catch (GameRunningException ex)
        {
            ActionStatus = ex.Message;
        }
        catch (Exception ex)
        {
            ActionStatus = $"{what}失败：{ex.Message}";
            Log.Warning(ex, "{What}失败", what);
        }
        finally
        {
            RefreshState();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private GameContext? GameContext => _settings.Current.GameDirectory is { Length: > 0 } directory
        ? new GameContext(directory)
        : null;

    /// <summary>同步汇报进度（<see cref="Progress{T}"/> 会异步派发，可能覆盖最终状态）。</summary>
    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
