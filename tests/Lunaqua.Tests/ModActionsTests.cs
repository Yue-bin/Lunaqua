using Lunaqua.Domain;
using Lunaqua.Infrastructure.OpenList;
using Lunaqua.Services;
using Lunaqua.ViewModels;
using Xunit;

namespace Lunaqua.Tests;

/// <summary>详情页动作（安装 / 更新 / 启停 / 卸载）走 VM 的端到端测试。</summary>
public sealed class ModActionsTests : IDisposable
{
    private readonly string _root;
    private readonly GameContext _game;
    private readonly SettingsService _settings;
    private readonly CacheStore _cache;

    public ModActionsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lunaqua-actions", Guid.NewGuid().ToString("N"));
        _game = new GameContext(Path.Combine(_root, "game"));
        Directory.CreateDirectory(_game.GameDirectory);

        _settings = new SettingsService(Path.Combine(_root, "settings.json"));
        _settings.Load();
        _settings.Update(settings => settings.GameDirectory = _game.GameDirectory);

        _cache = new CacheStore(Path.Combine(_root, "cache"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task 详情页能安装最新版并更新状态()
    {
        var dll = TestMod.Bytes("demo v1");
        var site = new FakeModSite().Add("/demo/demo.dll", dll);
        var version = TestMod.Release("1.0.0", ("demo.dll", dll));
        var meta = TestMod.Info(id: "demo", versions: version);
        var (detail, _, _) = CreateVms(site);

        await detail.LoadAsync(new CatalogEntry(meta, "demo", []));
        Assert.Equal("未安装", detail.StatusText);

        await detail.InstallLatestCommand.ExecuteAsync(null);

        Assert.Contains("完成", detail.ActionStatus);
        Assert.True(File.Exists(Path.Combine(_game.PluginsDirectory, "demo.dll")));
        Assert.True(detail.State!.IsInstalled);
        Assert.Equal("已装 v1.0.0", detail.StatusText);
        Assert.True(detail.Versions[0].IsInstalled);
    }

    [Fact]
    public async Task 没选游戏目录时给提示而不是崩()
    {
        var dll = TestMod.Bytes("demo");
        var site = new FakeModSite().Add("/demo/demo.dll", dll);
        var version = TestMod.Release("1.0.0", ("demo.dll", dll));
        var meta = TestMod.Info(id: "demo", versions: version);
        var (detail, _, _) = CreateVms(site);

        _settings.Update(settings => settings.GameDirectory = null);
        await detail.LoadAsync(new CatalogEntry(meta, "demo", []));

        await detail.InstallLatestCommand.ExecuteAsync(null);

        Assert.Contains("设置", detail.ActionStatus);
        Assert.False(File.Exists(Path.Combine(_game.PluginsDirectory, "demo.dll")));
    }

    [Fact]
    public async Task 游戏运行中安装会被拒绝并提示()
    {
        var dll = TestMod.Bytes("demo");
        var site = new FakeModSite().Add("/demo/demo.dll", dll);
        var version = TestMod.Release("1.0.0", ("demo.dll", dll));
        var meta = TestMod.Info(id: "demo", versions: version);
        var (detail, _, _) = CreateVms(site, gameRunning: true);

        await detail.LoadAsync(new CatalogEntry(meta, "demo", []));
        await detail.InstallLatestCommand.ExecuteAsync(null);

        Assert.Contains("游戏正在运行", detail.ActionStatus);
    }

    [Fact]
    public async Task 安装后列表显示已装且能看出可更新()
    {
        var v1 = TestMod.Bytes("demo v1");
        var v2 = TestMod.Bytes("demo v2");
        var site = new FakeModSite().Add("/demo/demo-v1.dll", v1).Add("/demo/demo-v2.dll", v2);
        var version1 = TestMod.Release("1.0.0", ("demo-v1.dll", v1));
        var version2 = TestMod.Release("2.0.0", ("demo-v2.dll", v2));
        var meta = TestMod.Info(id: "demo", versions: [version1, version2]);
        site.Add("/demo/info.json", TestMod.Bytes(TestMod.ToJson(meta)));
        var (detail, library, _) = CreateVms(site);

        await detail.LoadAsync(new CatalogEntry(meta, "demo", []));
        await detail.InstallVersionCommand.ExecuteAsync(detail.Versions.Single(item => item.Version.Version.ToString() == "1.0.0"));

        await library.RefreshCommand.ExecuteAsync(null);

        var item = Assert.Single(library.Items);
        Assert.True(item.IsInstalled);
        Assert.True(item.HasUpdate);
        Assert.Contains("可更新", item.StatusText);

        library.OnlyUpdatable = true;
        Assert.Single(library.Items);
    }

    [Fact]
    public async Task 详情页能禁用再启用()
    {
        var dll = TestMod.Bytes("demo");
        var site = new FakeModSite().Add("/demo/demo.dll", dll);
        var version = TestMod.Release("1.0.0", ("demo.dll", dll));
        var meta = TestMod.Info(id: "demo", versions: version);
        var (detail, _, _) = CreateVms(site);

        await detail.LoadAsync(new CatalogEntry(meta, "demo", []));
        await detail.InstallLatestCommand.ExecuteAsync(null);
        Assert.Equal("禁用", detail.ToggleEnabledText);

        await detail.ToggleEnabledCommand.ExecuteAsync(null);

        Assert.False(detail.State!.IsEnabled);
        Assert.Equal("启用", detail.ToggleEnabledText);
        Assert.True(File.Exists(Path.Combine(_game.DisabledDirectory, "demo", "demo.dll")));

        await detail.ToggleEnabledCommand.ExecuteAsync(null);

        Assert.True(detail.State!.IsEnabled);
        Assert.True(File.Exists(Path.Combine(_game.PluginsDirectory, "demo.dll")));
    }

    [Fact]
    public async Task 详情页能卸载()
    {
        var dll = TestMod.Bytes("demo");
        var site = new FakeModSite().Add("/demo/demo.dll", dll);
        var version = TestMod.Release("1.0.0", ("demo.dll", dll));
        var meta = TestMod.Info(id: "demo", versions: version);
        var (detail, _, _) = CreateVms(site);

        await detail.LoadAsync(new CatalogEntry(meta, "demo", []));
        await detail.InstallLatestCommand.ExecuteAsync(null);

        await detail.UninstallCommand.ExecuteAsync(null);

        Assert.False(detail.State!.IsInstalled);
        Assert.Equal("未安装", detail.StatusText);
        Assert.False(File.Exists(Path.Combine(_game.PluginsDirectory, "demo.dll")));
    }

    [Fact]
    public async Task 忽略此版本后不再提示可更新()
    {
        var v1 = TestMod.Bytes("demo v1");
        var v2 = TestMod.Bytes("demo v2");
        var site = new FakeModSite().Add("/demo/demo-v1.dll", v1).Add("/demo/demo-v2.dll", v2);
        var version1 = TestMod.Release("1.0.0", ("demo-v1.dll", v1));
        var version2 = TestMod.Release("2.0.0", ("demo-v2.dll", v2));
        var meta = TestMod.Info(id: "demo", versions: [version1, version2]);
        var (detail, _, _) = CreateVms(site);

        await detail.LoadAsync(new CatalogEntry(meta, "demo", []));
        await detail.InstallVersionCommand.ExecuteAsync(detail.Versions.Single(item => item.Version.Version.ToString() == "1.0.0"));
        Assert.True(detail.HasUpdate);

        await detail.IgnoreUpdateCommand.ExecuteAsync(null);

        Assert.False(detail.HasUpdate);
        Assert.False(detail.CanIgnoreUpdate);
    }

    private (ModDetailViewModel Detail, ModLibraryViewModel Library, InstallEngine Engine) CreateVms(FakeModSite site, bool gameRunning = false)
    {
        var client = new OpenListClient(site.CreateClient());
        var repository = new ModRepository(client);
        var credentials = new CredentialStore();
        var engine = new InstallEngine(client, new ModTypeStrategies(), credentials, _cache, new FakeProcessDetector { Running = gameRunning });
        var installed = new InstalledModService(credentials, engine);
        var queue = new TaskQueue();
        var detail = new ModDetailViewModel(repository, engine, installed, _settings, queue);
        var library = new ModLibraryViewModel(repository, installed, _settings, detail);

        return (detail, library, engine);
    }
}
