using Lunaqua.Domain;
using Lunaqua.Infrastructure.OpenList;
using Lunaqua.Services;
using Lunaqua.ViewModels;
using Xunit;

namespace Lunaqua.Tests;

public sealed class UpdateTests
{
    [Fact]
    public async Task 设置页检查到新版本会提示()
    {
        var updates = new FakeUpdateService { IsInstalled = true, AvailableUpdate = new AppUpdate("1.2.3", false, null) };
        var viewModel = CreateSettings(updates);

        await viewModel.CheckUpdateCommand.ExecuteAsync(null);

        Assert.Contains("1.2.3", viewModel.UpdateStatus);
        Assert.Equal(1, updates.CheckCount);
    }

    [Fact]
    public async Task 没有新版本时提示已是最新()
    {
        var updates = new FakeUpdateService { IsInstalled = true };
        var viewModel = CreateSettings(updates);

        await viewModel.CheckUpdateCommand.ExecuteAsync(null);

        Assert.Contains("最新版本", viewModel.UpdateStatus);
    }

    [Fact]
    public async Task 回退会下载并应用上一版()
    {
        var updates = new FakeUpdateService
        {
            IsInstalled = true,
            RollbackTarget = new AppUpdate("0.9.0", true, null),
        };
        var viewModel = CreateSettings(updates);

        await viewModel.RollbackCommand.ExecuteAsync(null);

        Assert.Equal(1, updates.DownloadCount);
        Assert.Equal(1, updates.ApplyCount);
        Assert.Contains("0.9.0", viewModel.UpdateStatus);
    }

    [Fact]
    public async Task 没有更旧版本时回退给提示()
    {
        var updates = new FakeUpdateService { IsInstalled = true };
        var viewModel = CreateSettings(updates);

        await viewModel.RollbackCommand.ExecuteAsync(null);

        Assert.Equal(0, updates.DownloadCount);
        Assert.Contains("没有更旧的版本", viewModel.UpdateStatus);
    }

    [Fact]
    public async Task 开发运行不是安装版时说明自更新不可用()
    {
        var viewModel = CreateSettings(new FakeUpdateService { IsInstalled = false });

        Assert.Contains("开发运行", viewModel.UpdateHint);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task 启动检查有新版本时显示横幅并能下载重启()
    {
        var updates = new FakeUpdateService { IsInstalled = true, AvailableUpdate = new AppUpdate("1.2.3", false, null) };
        var viewModel = CreateShell(updates, checkOnStartup: true);

        await viewModel.CheckUpdateOnStartupAsync();

        Assert.True(viewModel.HasUpdateBanner);
        Assert.Contains("1.2.3", viewModel.UpdateBanner);

        await viewModel.ApplyUpdateCommand.ExecuteAsync(null);

        Assert.Equal(1, updates.DownloadCount);
        Assert.Equal(1, updates.ApplyCount);
    }

    [Fact]
    public async Task 关了启动检查就不查()
    {
        var updates = new FakeUpdateService { IsInstalled = true, AvailableUpdate = new AppUpdate("1.2.3", false, null) };
        var viewModel = CreateShell(updates, checkOnStartup: false);

        await viewModel.CheckUpdateOnStartupAsync();

        Assert.False(viewModel.HasUpdateBanner);
        Assert.Equal(0, updates.CheckCount);
    }

    [Fact]
    public async Task 关掉横幅后不再显示()
    {
        var updates = new FakeUpdateService { IsInstalled = true, AvailableUpdate = new AppUpdate("1.2.3", false, null) };
        var viewModel = CreateShell(updates, checkOnStartup: true);
        await viewModel.CheckUpdateOnStartupAsync();

        viewModel.DismissUpdateCommand.Execute(null);

        Assert.False(viewModel.HasUpdateBanner);
        Assert.Null(viewModel.UpdateBanner);
    }

    private static SettingsViewModel CreateSettings(IUpdateService updates)
    {
        var settings = new SettingsService(Path.Combine(Path.GetTempPath(), "lunaqua-update", Guid.NewGuid().ToString("N"), "settings.json"));
        settings.Load();
        return new SettingsViewModel(settings, new DialogService(), new CacheStore(), updates);
    }

    private static MainWindowViewModel CreateShell(IUpdateService updates, bool checkOnStartup)
    {
        var root = Path.Combine(Path.GetTempPath(), "lunaqua-update", Guid.NewGuid().ToString("N"));
        var settings = new SettingsService(Path.Combine(root, "settings.json"));
        settings.Load();
        settings.Update(value => value.CheckUpdateOnStartup = checkOnStartup);

        var cache = new CacheStore(Path.Combine(root, "cache"));
        var detector = new FakeProcessDetector();
        var credentials = new CredentialStore();
        var strategies = new ModTypeStrategies();
        var queue = new TaskQueue();

        var settingsViewModel = new SettingsViewModel(settings, new DialogService(), cache, updates);
        var detail = new ModDetailViewModel(
            new ModRepository(new OpenListClient(FakeHttp.Create((_, _) => FakeHttp.Envelope(new { content = Array.Empty<object>(), total = 0, write = false })))),
            new InstallEngine(new OpenListClient(FakeHttp.Create((_, _) => FakeHttp.Envelope(null, 500, "nope"))), strategies, credentials, cache, detector),
            new InstalledModService(credentials, new InstallEngine(new OpenListClient(FakeHttp.Create((_, _) => FakeHttp.Envelope(null, 500, "nope"))), strategies, credentials, cache, detector)),
            settings,
            queue,
            TestVms.ConfigTab(settings));

        var library = new ModLibraryViewModel(
            new ModRepository(new OpenListClient(FakeHttp.Create((_, _) => FakeHttp.Envelope(new { content = Array.Empty<object>(), total = 0, write = false })))),
            new InstalledModService(credentials, new InstallEngine(new OpenListClient(FakeHttp.Create((_, _) => FakeHttp.Envelope(null, 500, "nope"))), strategies, credentials, cache, detector)),
            settings,
            detail);

        var wizard = new WizardViewModel(
            new Lunaqua.Infrastructure.Steam.SteamLibraryLocator(steamPathProvider: () => null),
            new GameCleanCheckService(GameFingerprintTableLoader.Load()),
            new BepInExInstaller(new FakePackSource(), new FakeLauncher(), BepInExPackManifestLoader.Load()),
            settings,
            new DialogService(),
            queue);

        return new MainWindowViewModel(new HomeViewModel(settings), library, wizard, settingsViewModel, queue, updates, settings);
    }

    private sealed class FakePackSource : IBepInExPackSource
    {
        public Task<string> AcquireAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Path.GetTempPath());
    }

    private sealed class FakeLauncher : IGameLauncher
    {
        public Task<GameLaunchResult> LaunchAndWaitForBepInExLogAsync(GameContext game, string logMarker, TimeSpan timeout, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GameLaunchResult(false, null, "测试不启动游戏"));
    }
}
