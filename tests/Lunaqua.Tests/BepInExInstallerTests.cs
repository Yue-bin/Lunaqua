using Lunaqua.Domain;
using Lunaqua.Services;
using Xunit;

namespace Lunaqua.Tests;

public sealed class BepInExPackTests
{
    [Fact]
    public void 能加载随程序发布的pack清单()
    {
        var manifest = BepInExPackManifestLoader.Load();

        Assert.Equal("5.4.23.5", manifest.Version);
        Assert.Equal("x64", manifest.Architecture);
        Assert.Equal(23, manifest.FileCount);
        Assert.Equal("BepInEx 5.4.23.5", manifest.LogMarker);
        Assert.Contains(manifest.Files, file => file.Path == "winhttp.dll");
        Assert.Contains(manifest.Files, file => file.Path == "BepInEx/config/BepInEx.cfg");
        Assert.All(manifest.Files, file => Assert.Equal(64, file.Sha256.Length));
    }

    [Fact]
    public void 校验能发现缺文件与改内容()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunaqua-pack", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "BepInEx", "core"));
        File.WriteAllText(Path.Combine(root, "a.dll"), "a");
        File.WriteAllText(Path.Combine(root, "BepInEx", "core", "b.dll"), "b");

        var manifest = new BepInExPackManifest(1, "1.0.0", "x64", "/zip", new string('a', 64), 1,
        [
            new BepInExPackFile("a.dll", TestMod.Sha256(TestMod.Bytes("a")), 1),
            new BepInExPackFile("BepInEx/core/b.dll", TestMod.Sha256(TestMod.Bytes("b")), 1),
            new BepInExPackFile("missing.dll", new string('b', 64), 5),
        ]);

        var errors = SiteBepInExPackSource.VerifyPack(manifest, root);
        Assert.Contains(errors, error => error.Contains("缺少文件 missing.dll"));

        File.WriteAllText(Path.Combine(root, "a.dll"), "改过了");
        errors = SiteBepInExPackSource.VerifyPack(manifest, root);
        Assert.Contains(errors, error => error.Contains("a.dll"));

        Directory.Delete(root, recursive: true);
    }
}

public sealed class BepInExInstallerTests : IDisposable
{
    private readonly string _root;
    private readonly string _packDirectory;
    private readonly GameContext _game;
    private readonly BepInExPackManifest _manifest;

    public BepInExInstallerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lunaqua-bep", Guid.NewGuid().ToString("N"));
        _packDirectory = Path.Combine(_root, "pack");
        _game = new GameContext(Path.Combine(_root, "game"));
        Directory.CreateDirectory(_game.GameDirectory);
        Directory.CreateDirectory(Path.Combine(_packDirectory, "BepInEx", "core"));

        File.WriteAllText(Path.Combine(_packDirectory, "winhttp.dll"), "新的 winhttp");
        File.WriteAllText(Path.Combine(_packDirectory, "BepInEx", "core", "BepInEx.dll"), "新的 core");

        _manifest = new BepInExPackManifest(1, "5.4.23.5", "x64", "/zip", new string('a', 64), 1,
        [
            new BepInExPackFile("winhttp.dll", TestMod.Sha256(TestMod.Bytes("新的 winhttp")), TestMod.Bytes("新的 winhttp").Length),
            new BepInExPackFile("BepInEx/core/BepInEx.dll", TestMod.Sha256(TestMod.Bytes("新的 core")), TestMod.Bytes("新的 core").Length),
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
    public async Task 部署成功并写状态()
    {
        var installer = CreateInstaller();

        var result = await installer.InstallAsync(_game, verifyByLaunch: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Message);
        Assert.Equal("新的 winhttp", File.ReadAllText(Path.Combine(_game.GameDirectory, "winhttp.dll")));
        Assert.Equal("新的 core", File.ReadAllText(Path.Combine(_game.GameDirectory, "BepInEx", "core", "BepInEx.dll")));
        Assert.True(BepInExInstaller.IsInstalled(_game));

        var state = installer.ReadState(_game);
        Assert.NotNull(state);
        Assert.Equal("5.4.23.5", state!.Version);
        Assert.Equal(2, state.Files.Count);
    }

    [Fact]
    public async Task 启动验证成功才算成功()
    {
        var installer = CreateInstaller(new FakeLauncher(success: true, log: "BepInEx 5.4.23.5 - StickFight"));

        var result = await installer.InstallAsync(_game, verifyByLaunch: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Contains("跑起来", result.Message);
        Assert.Contains("BepInEx 5.4.23.5", result.LogExcerpt);
    }

    [Fact]
    public async Task 验证失败会回滚旧文件并删掉新文件()
    {
        var oldWinhttp = Path.Combine(_game.GameDirectory, "winhttp.dll");
        File.WriteAllText(oldWinhttp, "旧的 winhttp");

        var installer = CreateInstaller(new FakeLauncher(success: false, log: "没有 BepInEx 日志"));

        var result = await installer.InstallAsync(_game, verifyByLaunch: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("已还原", result.Message);
        Assert.Equal("旧的 winhttp", File.ReadAllText(oldWinhttp));
        Assert.False(File.Exists(Path.Combine(_game.GameDirectory, "BepInEx", "core", "BepInEx.dll")));
        Assert.Null(installer.ReadState(_game));
    }

    [Fact]
    public async Task pack内容不符时不落位()
    {
        File.WriteAllText(Path.Combine(_packDirectory, "winhttp.dll"), "被篡改");
        var installer = CreateInstaller();

        var result = await installer.InstallAsync(_game, verifyByLaunch: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("校验失败", result.Message);
        Assert.False(File.Exists(Path.Combine(_game.GameDirectory, "winhttp.dll")));
    }

    [Fact]
    public void 没装过时IsInstalled为false()
    {
        Assert.False(BepInExInstaller.IsInstalled(_game));
    }

    private BepInExInstaller CreateInstaller(IGameLauncher? launcher = null) =>
        new(new FakePackSource(_packDirectory), launcher ?? new FakeLauncher(true, null), _manifest);

    private sealed class FakePackSource(string directory) : IBepInExPackSource
    {
        public Task<string> AcquireAsync(CancellationToken cancellationToken = default) => Task.FromResult(directory);
    }

    private sealed class FakeLauncher(bool success, string? log) : IGameLauncher
    {
        public Task<GameLaunchResult> LaunchAndWaitForBepInExLogAsync(
            GameContext game,
            string logMarker,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GameLaunchResult(success, log, success ? "OK" : "没看到日志"));
    }
}
