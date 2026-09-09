using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lunaqua.Domain;
using Lunaqua.Infrastructure.Steam;
using Lunaqua.Services;
using Serilog;

namespace Lunaqua.ViewModels;

/// <summary>BepInEx 代装向导：① 找到游戏 ② 自动安装 ③ 验证（规格书 §8）。</summary>
public sealed partial class WizardViewModel : ViewModelBase, IActivatablePage
{
    /// <summary>「柴」的 Steam AppID。</summary>
    public const string StickFightAppId = "674940";

    private readonly SteamLibraryLocator _locator;
    private readonly GameCleanCheckService _cleanCheck;
    private readonly BepInExInstaller _installer;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly TaskQueue _queue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Step1Badge))]
    private bool _step1Done;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Step2Badge))]
    private bool _step2Done;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Step3Badge))]
    private bool _step3Done;

    [ObservableProperty]
    private string _gameDirectoryText = "还没找到游戏";

    [ObservableProperty]
    private string _locateStatus = "点下面的按钮，自动从 Steam 库里找「柴」。";

    [ObservableProperty]
    private string _deployStatus = "找到游戏之后才能装。";

    [ObservableProperty]
    private string _verifyStatus = "装完会自动启动一次游戏，看 BepInEx 日志。";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canDeploy;

    [ObservableProperty]
    private string _deployButtonText = "开始代装";

    /// <summary>干净判定不通过时，给玩家「信任并继续」的入口。</summary>
    [ObservableProperty]
    private bool _needsTrust;

    public WizardViewModel(
        SteamLibraryLocator locator,
        GameCleanCheckService cleanCheck,
        BepInExInstaller installer,
        ISettingsService settings,
        IDialogService dialogs,
        TaskQueue queue)
    {
        _locator = locator;
        _cleanCheck = cleanCheck;
        _installer = installer;
        _settings = settings;
        _dialogs = dialogs;
        _queue = queue;

        if (_settings.Current.GameDirectory is { Length: > 0 } saved)
        {
            GameDirectoryText = saved;
        }
    }

    public override string Title => "BepInEx 代装";

    public string Step1Badge => Step1Done ? "已完成" : "待办";

    public string Step2Badge => Step2Done ? "已完成" : "待办";

    public string Step3Badge => Step3Done ? "已完成" : "待办";

    public string Hint => "三步全自动：找游戏 → 装 BepInEx → 启动验证。中间不用你改文件。";

    public GameContext? Game { get; private set; }

    public string? BuildId { get; private set; }

    /// <summary>进页面时如果设置里已经存了游戏目录，直接体检一遍。</summary>
    public async Task ActivateAsync()
    {
        if (Game is null && GameDirectoryText is { Length: > 0 } directory && Directory.Exists(directory))
        {
            await InspectAsync(directory, BuildId);
        }
    }

    [RelayCommand]
    private async Task LocateAsync()
    {
        IsBusy = true;
        LocateStatus = "正在从 Steam 库里找「柴」……";
        try
        {
            var location = await Task.Run(() => _locator.FindGame(StickFightAppId));
            if (location is null)
            {
                LocateStatus = "没自动找到。用下面的「手动选择目录」指给我。";
                return;
            }

            await InspectAsync(location.Directory, location.Manifest.BuildId);
        }
        catch (Exception ex)
        {
            LocateStatus = $"定位失败：{ex.Message}";
            Log.Warning(ex, "定位游戏失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PickDirectoryAsync()
    {
        string? folder;
        try
        {
            folder = await _dialogs.PickFolderAsync("选择「柴」的游戏目录", GameDirectoryText);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "打开目录选择器失败");
            return;
        }

        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        await InspectAsync(folder, null);
    }

    [RelayCommand]
    private async Task DeployAsync()
    {
        if (Game is not { } game)
        {
            DeployStatus = "先完成第 1 步：找到游戏。";
            return;
        }

        IsBusy = true;
        Step2Done = false;
        Step3Done = false;
        DeployStatus = "正在准备 BepInEx pack……";
        VerifyStatus = "等待安装完成。";

        try
        {
            var result = await _queue.RunAsync(() => _installer.InstallAsync(
                game,
                verifyByLaunch: true,
                verifyTimeout: TimeSpan.FromSeconds(90)));

            Step2Done = result.Success || File.Exists(Path.Combine(game.GameDirectory, "winhttp.dll"));
            Step3Done = result.Success;
            DeployStatus = result.Success
                ? $"BepInEx {_installer.Manifest.Version} 已装好（{result.DeployedFiles?.Count ?? 0} 个文件）。"
                : result.Message;
            VerifyStatus = result.Success
                ? "日志里已经看到 BepInEx，可以进游戏了。"
                : result.Message;

            if (!result.Success && result.LogExcerpt is { Length: > 0 } excerpt)
            {
                VerifyStatus = result.Message + Environment.NewLine + Environment.NewLine + excerpt;
            }

            DeployButtonText = result.Success ? "重新安装 / 修复" : "再试一次";
        }
        catch (Exception ex)
        {
            DeployStatus = $"代装失败：{ex.Message}";
            Log.Warning(ex, "代装失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>把当前文件哈希记成本机信任基线（干净判定第三层）。</summary>
    [RelayCommand]
    private async Task TrustAsync()
    {
        if (Game is not { } game || GameDirectoryText is not { Length: > 0 } directory)
        {
            return;
        }

        var installation = GameInstallationValidator.Validate(directory);
        if (!installation.IsValid)
        {
            LocateStatus = installation.Summary;
            return;
        }

        await _cleanCheck.CaptureBaselineAsync(installation, BuildId);
        await InspectAsync(directory, BuildId);
        LocateStatus += "（已记住本机基线）";
    }

    [RelayCommand]
    private void OpenSteamValidate() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"steam://validate/{StickFightAppId}")
        {
            UseShellExecute = true,
        });

    [RelayCommand]
    private void OpenGameDirectory()
    {
        if (Game is { } game)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(game.GameDirectory) { UseShellExecute = true });
        }
    }

    /// <summary>体检：结构 + 位数 + 干净判定。</summary>
    private async Task InspectAsync(string directory, string? buildId)
    {
        GameDirectoryText = directory;
        BuildId = buildId;
        Step1Done = false;
        Step2Done = false;
        Step3Done = false;
        CanDeploy = false;
        DeployButtonText = "开始代装";

        var installation = GameInstallationValidator.Validate(directory);
        if (!installation.IsValid)
        {
            LocateStatus = installation.Summary;
            DeployStatus = "目录不合法，先按提示处理。";
            return;
        }

        Game = new GameContext(directory);
        _settings.Update(settings => settings.GameDirectory = directory);

        var clean = await _cleanCheck.CheckAsync(installation, buildId);
        NeedsTrust = clean.Status == GameCleanStatus.NeedsValidate;
        Step1Done = true;
        CanDeploy = true;
        DeployStatus = BepInExInstaller.IsInstalled(Game)
            ? "这个目录里已经有 BepInEx 了，可以重新安装 / 修复。"
            : "可以开始代装了。";

        LocateStatus = buildId is { Length: > 0 }
            ? $"{installation.ExecutablePath}（64 位）· buildid {buildId} · {clean.Message}"
            : $"{installation.ExecutablePath}（64 位）· {clean.Message}";
    }
}
