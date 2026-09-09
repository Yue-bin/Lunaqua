using Lunaqua.Domain;
using Lunaqua.Services;
using Xunit;

namespace Lunaqua.Tests;

/// <summary>
/// 真机验证（默认跳过）：设了 LUNAQUA_TEST_PACK_DIR（已解包的 pack 目录）和
/// LUNAQUA_TEST_GAME_DIR（一份干净的游戏副本）才跑 —— 会真的启动游戏并轮询日志。
/// </summary>
public sealed class BepInExLiveTests
{
    [Fact]
    public async Task 真机部署pack并跑起来()
    {
        var packDirectory = Environment.GetEnvironmentVariable("LUNAQUA_TEST_PACK_DIR");
        var gameDirectory = Environment.GetEnvironmentVariable("LUNAQUA_TEST_GAME_DIR");

        if (string.IsNullOrWhiteSpace(packDirectory) || string.IsNullOrWhiteSpace(gameDirectory))
        {
            Assert.Skip("没设 LUNAQUA_TEST_PACK_DIR / LUNAQUA_TEST_GAME_DIR，跳过真机验证");
            return;
        }

        var manifest = BepInExPackManifestLoader.Load();
        var installer = new BepInExInstaller(new LocalPackSource(packDirectory), new ProcessGameLauncher(), manifest);
        var game = new GameContext(gameDirectory);

        var result = await installer.InstallAsync(
            game,
            verifyByLaunch: true,
            verifyTimeout: TimeSpan.FromSeconds(60),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Message + "\n" + (result.LogExcerpt ?? "(没有日志)"));
        Assert.Contains("BepInEx 5.4.23.5", result.LogExcerpt);
        Assert.True(File.Exists(Path.Combine(gameDirectory, "BepInEx", "LogOutput.log")));
    }

    private sealed class LocalPackSource(string directory) : IBepInExPackSource
    {
        public Task<string> AcquireAsync(CancellationToken cancellationToken = default) => Task.FromResult(directory);
    }
}
