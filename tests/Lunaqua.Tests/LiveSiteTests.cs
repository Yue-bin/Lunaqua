using Lunaqua.Domain;
using Lunaqua.Infrastructure.OpenList;
using Lunaqua.Services;
using Xunit;

namespace Lunaqua.Tests;

/// <summary>对真实站点的只读联调（站点不可达时跳过，不算失败）。</summary>
public sealed class LiveSiteTests
{
    [Fact]
    public async Task 站上目录能列出全部mod()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var repository = new ModRepository(new OpenListClient(http));

        ModCatalog catalog;
        try
        {
            catalog = await repository.LoadCatalogAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OpenListException)
        {
            Assert.Skip($"站点不可达，跳过联调：{ex.Message}");
            return;
        }

        Assert.Empty(catalog.Issues);
        Assert.True(catalog.ModCount >= 9, $"站上只列出 {catalog.ModCount} 个 mod");

        var playermanager = catalog.Entries.Single(entry => entry.Info.Id == "stick.plugins.playermanager");
        Assert.Equal("玩家管理器", playermanager.Info.Name);
        Assert.Equal("5.1.0", playermanager.Info.Latest.ToString());
        Assert.True(playermanager.Info.Versions.Count >= 10);

        Assert.All(catalog.Entries, entry =>
        {
            Assert.Equal(ModTypes.BepInEx, entry.Info.Type);
            Assert.NotEmpty(entry.Info.Versions);
            Assert.NotNull(entry.Info.FindVersion(entry.Info.Latest));
        });
    }

    [Fact]
    public async Task 能读到mod的readme()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var repository = new ModRepository(new OpenListClient(http));

        string? readme;
        try
        {
            readme = await repository.ReadReadmeAsync("stick.plugins.playermanager", TestContext.Current.CancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OpenListException)
        {
            Assert.Skip($"站点不可达，跳过联调：{ex.Message}");
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(readme));
    }
}
