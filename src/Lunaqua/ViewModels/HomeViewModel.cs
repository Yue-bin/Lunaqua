using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lunaqua.Services;

namespace Lunaqua.ViewModels;

/// <summary>首页：状态卡 + 快捷操作 + 最近更新。</summary>
public sealed partial class HomeViewModel : ViewModelBase
{
    /// <summary>「柴」的 Steam AppID。</summary>
    public const string StickFightAppId = "674940";

    private readonly ISettingsService _settings;

    [ObservableProperty]
    private string _gameDirectoryText = "未设置";

    public HomeViewModel(ISettingsService settings)
    {
        _settings = settings;
        _settings.Changed += (_, _) => Refresh();
        Refresh();
    }

    public override string Title => "首页";

    /// <summary>BepInEx 状态（M3 代装里程碑接入检测）。</summary>
    public string BepInExStatusText => "未检测";

    /// <summary>可更新 mod 数（M1 接入站点后统计）。</summary>
    public string UpdateCountText => "—";

    /// <summary>最近更新空状态文案。</summary>
    public string RecentChangesHint => "还没有记录。装过 mod 之后，这里会显示最近更新。";

    public bool HasGameDirectory => !string.IsNullOrWhiteSpace(_settings.Current.GameDirectory);

    [RelayCommand(CanExecute = nameof(CanOpenModFolder))]
    private void OpenModFolder()
    {
        if (_settings.Current.GameDirectory is not { Length: > 0 } game)
        {
            return;
        }

        var plugins = Path.Combine(game, "BepInEx", "plugins");
        Process.Start(new ProcessStartInfo(Directory.Exists(plugins) ? plugins : game) { UseShellExecute = true });
    }

    private bool CanOpenModFolder() => HasGameDirectory;

    [RelayCommand]
    private void LaunchGame() =>
        Process.Start(new ProcessStartInfo($"steam://rungameid/{StickFightAppId}") { UseShellExecute = true });

    private void Refresh()
    {
        GameDirectoryText = _settings.Current.GameDirectory is { Length: > 0 } game ? game : "未设置";
        OnPropertyChanged(nameof(HasGameDirectory));
        OpenModFolderCommand.NotifyCanExecuteChanged();
    }
}
