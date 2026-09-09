using Lunaqua.Domain;
using Lunaqua.Infrastructure.OpenList;
using Lunaqua.Services;
using Xunit;

namespace Lunaqua.Tests;

public sealed class InstallEngineTests : IDisposable
{
    private readonly string _root;
    private readonly string _cacheRoot;
    private readonly GameContext _game;

    public InstallEngineTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lunaqua-engine", Guid.NewGuid().ToString("N"));
        _cacheRoot = Path.Combine(_root, "cache");
        _game = new GameContext(Path.Combine(_root, "game"));
        Directory.CreateDirectory(_game.GameDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task 安装后文件落位并写凭证()
    {
        var dll = TestMod.Bytes("demo v1");
        var site = new FakeModSite().Add("/demo/demo-v1.dll", dll);
        var version = TestMod.Release("1.0.0", ("demo-v1.dll", dll));
        var meta = TestMod.Info(id: "demo", versions: version);

        var engine = CreateEngine(site);
        var credential = await engine.InstallAsync(_game, "demo", meta, version, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(_game.PluginsDirectory, "demo-v1.dll")));
        Assert.True(File.Exists(Path.Combine(_game.LunaquaDirectory, "demo", "info.json")));
        Assert.Equal("1.0.0", credential.Version);
        Assert.True(credential.Enabled);
        Assert.Equal(ModSource.Official, credential.Source);
        Assert.Single(credential.Files);
    }

    [Fact]
    public async Task 更新时删掉旧版本多出来的文件()
    {
        var v1 = TestMod.Bytes("demo v1");
        var v2 = TestMod.Bytes("demo v2");
        var site = new FakeModSite().Add("/demo/demo-v1.dll", v1).Add("/demo/demo-v2.dll", v2);
        var version1 = TestMod.Release("1.0.0", ("demo-v1.dll", v1));
        var version2 = TestMod.Release("2.0.0", ("demo-v2.dll", v2));
        var meta = TestMod.Info(id: "demo", versions: [version1, version2]);

        var engine = CreateEngine(site);
        var token = TestContext.Current.CancellationToken;
        await engine.InstallAsync(_game, "demo", meta, version1, cancellationToken: token);
        var updated = await engine.InstallAsync(_game, "demo", meta, version2, cancellationToken: token);

        Assert.False(File.Exists(Path.Combine(_game.PluginsDirectory, "demo-v1.dll")));
        Assert.True(File.Exists(Path.Combine(_game.PluginsDirectory, "demo-v2.dll")));
        Assert.Equal("2.0.0", updated.Version);
    }

    [Fact]
    public async Task 落位失败时整体回滚且不写凭证()
    {
        var ok = TestMod.Bytes("ok");
        var blocked = TestMod.Bytes("blocked");
        var site = new FakeModSite().Add("/demo/ok.dll", ok).Add("/demo/blocked.dll", blocked);
        var version = TestMod.Release("1.0.0", ("ok.dll", ok), ("blocked.dll", blocked));
        var meta = TestMod.Info(id: "demo", versions: version);

        // 用同名目录把第二个目标路径占住，让 File.Copy 抛 IOException
        Directory.CreateDirectory(Path.Combine(_game.PluginsDirectory, "blocked.dll"));

        var engine = CreateEngine(site);
        await Assert.ThrowsAnyAsync<IOException>(() => engine.InstallAsync(_game, "demo", meta, version, cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(File.Exists(Path.Combine(_game.PluginsDirectory, "ok.dll")));
        Assert.False(File.Exists(Path.Combine(_game.LunaquaDirectory, "demo", "info.json")));
    }

    [Fact]
    public async Task 哈希不符时抛错且不落位()
    {
        var site = new FakeModSite().Add("/demo/demo.dll", TestMod.Bytes("真实内容"));
        var version = TestMod.Release("1.0.0", ("demo.dll", TestMod.Bytes("期望内容")));
        var meta = TestMod.Info(id: "demo", versions: version);

        var engine = CreateEngine(site);
        await Assert.ThrowsAsync<InvalidDataException>(() => engine.InstallAsync(_game, "demo", meta, version, cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(File.Exists(Path.Combine(_game.PluginsDirectory, "demo.dll")));
        Assert.False(File.Exists(Path.Combine(_game.LunaquaDirectory, "demo", "info.json")));
    }

    [Fact]
    public async Task 卸载删文件与凭证()
    {
        var dll = TestMod.Bytes("demo");
        var site = new FakeModSite().Add("/demo/demo.dll", dll);
        var version = TestMod.Release("1.0.0", ("demo.dll", dll));
        var meta = TestMod.Info(id: "demo", versions: version);

        var engine = CreateEngine(site);
        var token = TestContext.Current.CancellationToken;
        var credential = await engine.InstallAsync(_game, "demo", meta, version, cancellationToken: token);

        engine.Uninstall(_game, credential);

        Assert.False(File.Exists(Path.Combine(_game.PluginsDirectory, "demo.dll")));
        Assert.False(File.Exists(Path.Combine(_game.LunaquaDirectory, "demo", "info.json")));
    }

    [Fact]
    public async Task 禁用与启用会搬移文件()
    {
        var dll = TestMod.Bytes("demo");
        var site = new FakeModSite().Add("/demo/demo.dll", dll);
        var version = TestMod.Release("1.0.0", ("demo.dll", dll));
        var meta = TestMod.Info(id: "demo", versions: version);

        var engine = CreateEngine(site);
        var token = TestContext.Current.CancellationToken;
        var credential = await engine.InstallAsync(_game, "demo", meta, version, cancellationToken: token);

        var disabled = engine.SetEnabled(_game, credential, false);

        Assert.False(disabled.Enabled);
        Assert.False(File.Exists(Path.Combine(_game.PluginsDirectory, "demo.dll")));
        Assert.True(File.Exists(Path.Combine(_game.DisabledDirectory, "demo", "demo.dll")));
        Assert.False(engine.DetectEnabled(_game, disabled));

        var enabled = engine.SetEnabled(_game, disabled, true);

        Assert.True(enabled.Enabled);
        Assert.True(File.Exists(Path.Combine(_game.PluginsDirectory, "demo.dll")));
        Assert.False(Directory.Exists(Path.Combine(_game.DisabledDirectory, "demo")));
        Assert.True(engine.DetectEnabled(_game, enabled));
    }

    [Fact]
    public async Task 游戏运行时拒绝安装与卸载()
    {
        var dll = TestMod.Bytes("demo");
        var site = new FakeModSite().Add("/demo/demo.dll", dll);
        var version = TestMod.Release("1.0.0", ("demo.dll", dll));
        var meta = TestMod.Info(id: "demo", versions: version);

        var engine = CreateEngine(site, gameRunning: true);
        await Assert.ThrowsAsync<GameRunningException>(() => engine.InstallAsync(_game, "demo", meta, version, cancellationToken: TestContext.Current.CancellationToken));

        var idle = CreateEngine(site);
        var credential = await idle.InstallAsync(_game, "demo", meta, version, cancellationToken: TestContext.Current.CancellationToken);

        var running = CreateEngine(site, gameRunning: true);
        Assert.Throws<GameRunningException>(() => running.Uninstall(_game, credential));
        Assert.Throws<GameRunningException>(() => running.SetEnabled(_game, credential, false));
    }

    [Fact]
    public async Task 缓存命中时不重复下载()
    {
        var dll = TestMod.Bytes("demo");
        var site = new FakeModSite().Add("/demo/demo.dll", dll);
        var version = TestMod.Release("1.0.0", ("demo.dll", dll));
        var meta = TestMod.Info(id: "demo", versions: version);

        var engine = CreateEngine(site);

        var token = TestContext.Current.CancellationToken;
        await engine.InstallAsync(_game, "demo", meta, version, cancellationToken: token);
        var afterFirst = site.Requests;

        engine.Uninstall(_game, new CredentialStore().Read(Path.Combine(_game.LunaquaDirectory, "demo", "info.json"))!);
        await engine.InstallAsync(_game, "demo", meta, version, cancellationToken: token);

        Assert.Equal(afterFirst, site.Requests);
    }

    private InstallEngine CreateEngine(FakeModSite site, bool gameRunning = false) => new(
        new OpenListClient(site.CreateClient()),
        new ModTypeStrategies(),
        new CredentialStore(),
        new CacheStore(_cacheRoot),
        new FakeProcessDetector { Running = gameRunning });
}
