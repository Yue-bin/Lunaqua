using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lunaqua.Domain;
using Lunaqua.Infrastructure.BepInEx;
using Lunaqua.Services;
using Serilog;

namespace Lunaqua.ViewModels;

/// <summary>详情页「配置」tab：按 GUID 关联 .cfg，按类型生成控件（规格书 §9）。</summary>
public sealed partial class ConfigTabViewModel : ViewModelBase
{
    private readonly ConfigEditorService _service;
    private readonly IGameProcessDetector _processDetector;
    private readonly ISettingsService _settings;
    private readonly List<ConfigEntryViewModel> _all = [];

    [ObservableProperty]
    private bool _hasConfig;

    [ObservableProperty]
    private bool _isReadOnly;

    [ObservableProperty]
    private bool _onlyChanged;

    [ObservableProperty]
    private string _statusText = "选一个 mod 就能看到它的配置。";

    [ObservableProperty]
    private string _filePathText = string.Empty;

    [ObservableProperty]
    private bool _hasSelection;

    public ConfigTabViewModel(
        ConfigEditorService service,
        IGameProcessDetector processDetector,
        ISettingsService settings)
    {
        _service = service;
        _processDetector = processDetector;
        _settings = settings;
    }

    public override string Title => "配置";

    public ObservableCollection<ConfigEntryViewModel> Entries { get; } = [];

    public bool HasNoConfig => HasSelection && !HasConfig;

    /// <summary>顶部常驻提示。</summary>
    public string EffectHint => "配置在下次启动游戏时生效。";

    /// <summary>载入某个 mod 的配置；<paramref name="guid"/> 为空时按 .cfg 文件名兜底。</summary>
    public void Load(string? guid, string? modId)
    {
        HasSelection = true;
        _all.Clear();
        Entries.Clear();
        HasConfig = false;
        IsReadOnly = _service.IsGameRunning;

        if (GameContext is not { } game)
        {
            StatusText = "先去「设置」里选游戏目录。";
            return;
        }

        var file = _service.FindForGuid(game, guid)
            ?? (modId is { Length: > 0 } ? _service.FindForGuid(game, modId) : null);

        if (file is null)
        {
            StatusText = "还没生成配置。先启动一次游戏，BepInEx 会把 .cfg 写出来。";
            FilePathText = string.Empty;
            OnPropertyChanged(nameof(HasNoConfig));
            return;
        }

        FilePathText = file.Path;

        foreach (var entry in file.Entries)
        {
            _all.Add(new ConfigEntryViewModel(file, entry, _service, game, IsReadOnly));
        }

        HasConfig = _all.Count > 0;
        StatusText = HasConfig
            ? $"{_all.Count} 项配置" + (IsReadOnly ? "（游戏运行中，只读）" : string.Empty)
            : "这个配置文件里没有可编辑的条目。";

        ApplyFilter();
        OnPropertyChanged(nameof(HasNoConfig));
    }

    [RelayCommand]
    private void ResetAll()
    {
        foreach (var entry in _all)
        {
            entry.Reset();
        }

        ApplyFilter();
    }

    [RelayCommand]
    private void LaunchGame() =>
        Process.Start(new ProcessStartInfo($"steam://rungameid/{WizardViewModel.StickFightAppId}") { UseShellExecute = true });

    [RelayCommand]
    private void Reload()
    {
        if (GameContext is { } game && FilePathText is { Length: > 0 } path)
        {
            Load(CfgFile.Parse(File.ReadAllText(path), path).PluginGuid, Path.GetFileNameWithoutExtension(path));
            return;
        }

        Log.Debug("配置重载：当前没有文件");
    }

    partial void OnOnlyChangedChanged(bool value) => ApplyFilter();

    private void ApplyFilter()
    {
        Entries.Clear();
        foreach (var entry in _all.Where(entry => !OnlyChanged || entry.IsChanged))
        {
            Entries.Add(entry);
        }
    }

    private GameContext? GameContext => _settings.Current.GameDirectory is { Length: > 0 } directory
        ? new GameContext(directory)
        : null;
}
