using Lunaqua.Services;
using Velopack.Locators;
using Velopack.Sources;
using Xunit;

namespace Lunaqua.Tests;

/// <summary>用真实打包产物联调更新检查（设了 LUNAQUA_TEST_RELEASES 才跑）。</summary>
public sealed class UpdateLiveTests
{
    [Fact]
    public async Task 能从真实打包产物里检查到更新()
    {
        var releases = Environment.GetEnvironmentVariable("LUNAQUA_TEST_RELEASES");
        if (string.IsNullOrWhiteSpace(releases) || !File.Exists(Path.Combine(releases, "releases.win.json")))
        {
            Assert.Skip("没设 LUNAQUA_TEST_RELEASES，跳过真实更新源联调");
            return;
        }

        var packagesDir = Path.Combine(Path.GetTempPath(), "lunaqua-update-live", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(packagesDir);

        // 假装已经装了 0.0.1，看能不能从真实产物里发现 0.1.0
        var locator = new TestVelopackLocator("Lunaqua", "0.0.1", packagesDir);
        var service = new VelopackUpdateService(new SimpleFileSource(new DirectoryInfo(releases)), locator);

        var update = await service.CheckAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(update);
        Assert.Equal("0.1.0", update!.Version);
        Assert.False(update.IsDowngrade);

        Directory.Delete(packagesDir, recursive: true);
    }
}
