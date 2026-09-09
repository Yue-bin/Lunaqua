using Lunaqua.Domain;
using Lunaqua.Infrastructure.Steam;
using Lunaqua.Services;
using Lunaqua.ViewModels;
using Xunit;

namespace Lunaqua.Tests;

public sealed class WizardViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly string _steamPath;
    private readonly string _gameDirectory;
    private readonly string _packDirectory;
    private readonly BepInExPackManifest _manifest;

    public WizardViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lunaqua-wizard", Guid.NewGuid().ToString("N"));
        _steamPath = Path.Combine(_root, "Steam");
        _gameDirectory = Path.Combine(_root, "Steam", "steamapps", "common", "StickFightTheGame");
        _packDirectory = Path.Combine(_root, "pack");

        Directory.CreateDirectory(Path.Combine(_steamPath, "steamapps"));
        Directory.CreateDirectory(Path.Combine(_gameDirectory, "StickFight_Data", "Managed"));
        Directory.CreateDirectory(Path.Combine(_packDirectory, "BepInEx", "core"));

        File.WriteAllBytes(Path.Combine(_gameDirectory, "StickFight.exe"), TestPe.Create(TestPe.MachineAmd64));
        File.WriteAllText(Path.Combine(_gameDirectory, "StickFight_Data", "Managed", "Assembly-CSharp.dll"), "官方");
        File.WriteAllText(Path.Combine(_packDirectory, "winhttp.dll"), "winhttp");
        File.WriteAllText(Path.Combine(_packDirectory, "BepInEx", "core", "BepInEx.dll"), "core");

        File.WriteAllText(
            Path.Combine(_steamPath, "steamapps", "libraryfolders.vdf"),
            $$"""
              "libraryfolders"
              {
                  "0"
                  {
                      "path"		"{{_steamPath.Replace("\\", "\\\\")}}"
                  }
              }
              """);

        File.WriteAllText(
            Path.Combine(_steamPath, "steamapps", "appmanifest_674940.acf"),
            """
            "AppState"
            {
                "appid"		"674940"
                "installdir"		"StickFightTheGame"
                "buildid"		"24952802"
                "StateFlags"		"4"
            }
            """);

        _manifest = new BepInExPackManifest(1, "5.4.23.5", "x64", "/zip", new string('a', 64), 1,
        [
            new BepInExPackFile("winhttp.dll", TestMod.Sha256(TestMod.Bytes("winhttp")), 7),
            new BepInExPackFile("BepInEx/core/BepInEx.dll", TestMod.Sha256(TestMod.Bytes("core")), 4),
        ]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task 自动定位后完成第1步并允许部署()
    {
        var viewModel = CreateViewModel();

        await viewModel.LocateCommand.ExecuteAsync(null);

        Assert.True(viewModel.Step1Done);
        Assert.True(viewModel.CanDeploy);
        Assert.Contains("buildid 24952802", viewModel.LocateStatus);
        Assert.Contains("StickFight.exe", viewModel.LocateStatus);
    }

    [Fact]
    public async Task 找不到游戏时提示手动选目录()
    {
        File.Delete(Path.Combine(_steamPath, "steamapps", "appmanifest_674940.acf"));
        var viewModel = CreateViewModel();

        await viewModel.LocateCommand.ExecuteAsync(null);

        Assert.False(viewModel.Step1Done);
        Assert.False(viewModel.CanDeploy);
        Assert.Contains("手动选择目录", viewModel.LocateStatus);
    }

    [Fact]
    public async Task 没完成第一步时点部署给提示()
    {
        var viewModel = CreateViewModel();

        await viewModel.DeployCommand.ExecuteAsync(null);

        Assert.Contains("第 1 步", viewModel.DeployStatus);
    }

    [Fact]
    public async Task 指纹不认识时可以信任本机基线()
    {
        var manifestPath = Path.Combine(_steamPath, "steamapps", "appmanifest_674940.acf");
        File.WriteAllText(manifestPath, """
            "AppState"
            {
                "appid"		"674940"
                "installdir"		"StickFightTheGame"
                "buildid"		"99999999"
                "StateFlags"		"4"
            }
            """);

        var viewModel = CreateViewModel();
        await viewModel.LocateCommand.ExecuteAsync(null);

        Assert.True(viewModel.NeedsTrust);
        Assert.Contains("Steam 校验", viewModel.LocateStatus);

        await viewModel.TrustCommand.ExecuteAsync(null);

        Assert.False(viewModel.NeedsTrust);
        Assert.Contains("基线", viewModel.LocateStatus);
    }

    [Fact]
    public async Task 部署成功后第2和第3步都完成()
    {
        var viewModel = CreateViewModel();

        await viewModel.LocateCommand.ExecuteAsync(null);
        await viewModel.DeployCommand.ExecuteAsync(null);

        Assert.True(viewModel.Step2Done);
        Assert.True(viewModel.Step3Done);
        Assert.Contains("5.4.23.5", viewModel.DeployStatus);
        Assert.Contains("日志里已经看到 BepInEx", viewModel.VerifyStatus);
        Assert.Equal("重新安装 / 修复", viewModel.DeployButtonText);
        Assert.True(File.Exists(Path.Combine(_gameDirectory, "winhttp.dll")));
    }

    [Fact]
    public async Task 部署失败时提示并保留第1步状态()
    {
        var viewModel = CreateViewModel(launchSuccess: false);

        await viewModel.LocateCommand.ExecuteAsync(null);
        await viewModel.DeployCommand.ExecuteAsync(null);

        Assert.True(viewModel.Step1Done);
        Assert.False(viewModel.Step3Done);
        Assert.Contains("已还原", viewModel.DeployStatus);
        Assert.Equal("再试一次", viewModel.DeployButtonText);
        Assert.False(File.Exists(Path.Combine(_gameDirectory, "winhttp.dll")));
    }

    private WizardViewModel CreateViewModel(bool launchSuccess = true)
    {
        var locator = new SteamLibraryLocator(steamPathProvider: () => _steamPath);
        var cleanCheck = new GameCleanCheckService(GameFingerprintTableLoader.Load());
        var installer = new BepInExInstaller(
            new FakePackSource(_packDirectory),
            new FakeLauncher(launchSuccess),
            _manifest);

        var settings = new SettingsService(Path.Combine(_root, "settings.json"));
        settings.Load();

        return new WizardViewModel(locator, cleanCheck, installer, settings, new DialogService(), new TaskQueue());
    }

    private sealed class FakePackSource(string directory) : IBepInExPackSource
    {
        public Task<string> AcquireAsync(CancellationToken cancellationToken = default) => Task.FromResult(directory);
    }

    private sealed class FakeLauncher(bool success) : IGameLauncher
    {
        public Task<GameLaunchResult> LaunchAndWaitForBepInExLogAsync(
            GameContext game,
            string logMarker,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GameLaunchResult(success, success ? "BepInEx 5.4.23.5 - StickFight" : null, success ? "OK" : "没看到日志"));
    }
}
