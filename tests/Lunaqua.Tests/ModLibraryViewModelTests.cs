using Lunaqua.Infrastructure.OpenList;
using Lunaqua.Services;
using Lunaqua.ViewModels;
using Xunit;

namespace Lunaqua.Tests;

public sealed class ModLibraryViewModelTests
{
    private static readonly Dictionary<string, string> Site = new(StringComparer.Ordinal)
    {
        ["stick.plugins.demo"] = TestMetadata.Json(id: "stick.plugins.demo", name: "演示插件", author: "某人", description: "演示用。"),
        ["stick.plugins.playermanager"] = TestMetadata.Json(id: "stick.plugins.playermanager", name: "玩家管理器", author: "老狼", description: "房间查找。"),
    };

    [Fact]
    public async Task 刷新后列出全部mod()
    {
        var library = CreateLibrary(FakeSite.Create(Site));

        await library.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, library.Items.Count);
        Assert.Contains("2 个 mod", library.StatusText);
        Assert.False(library.HasError);
    }

    [Fact]
    public async Task 搜索能按名字作者与简介过滤()
    {
        var library = CreateLibrary(FakeSite.Create(Site));
        await library.RefreshCommand.ExecuteAsync(null);

        library.SearchText = "玩家";
        Assert.Single(library.Items);
        Assert.Equal("玩家管理器", library.Items[0].Name);
        Assert.Contains("筛选出 1 / 2", library.StatusText);

        library.SearchText = "老狼";
        Assert.Single(library.Items);

        library.SearchText = "房间";
        Assert.Single(library.Items);

        library.SearchText = "不存在的关键词";
        Assert.Empty(library.Items);
        Assert.False(library.HasItems);
    }

    [Fact]
    public async Task 选中后详情载入并读取readme()
    {
        var library = CreateLibrary(FakeSite.Create(Site));
        await library.RefreshCommand.ExecuteAsync(null);

        library.SelectedItem = library.Items.Single(item => item.Name == "玩家管理器");

        await WaitUntilAsync(() => library.Detail.HasSelection && !library.Detail.IsLoadingReadme);

        Assert.Equal("玩家管理器", library.Detail.Name);
        Assert.Equal("老狼", library.Detail.Author);
        Assert.Single(library.Detail.Versions);
        Assert.True(library.Detail.Versions[0].IsLatest);
        Assert.Contains("说明", library.Detail.ReadmeText);
    }

    [Fact]
    public async Task 站点出错时显示错误而不是崩溃()
    {
        var library = CreateLibrary(FakeHttp.Create((_, _) => FakeHttp.Text("<html>SPA</html>", "text/html")));

        await library.RefreshCommand.ExecuteAsync(null);

        Assert.True(library.HasError);
        Assert.Contains("读取站点目录失败", library.ErrorText);
        Assert.Empty(library.Items);
    }

    [Fact]
    public async Task id不一致的mod会标黄()
    {
        var site = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["other.dir"] = TestMetadata.Json(id: "stick.plugins.demo", name: "名字对不上"),
        };

        var library = CreateLibrary(FakeSite.Create(site));
        await library.RefreshCommand.ExecuteAsync(null);

        var item = Assert.Single(library.Items);
        Assert.True(item.HasWarning);
        Assert.Contains("不一致", item.WarningText);
    }

    private static ModLibraryViewModel CreateLibrary(HttpClient http)
    {
        var repository = new ModRepository(new OpenListClient(http));
        return new ModLibraryViewModel(repository, new ModDetailViewModel(repository));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 10000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(condition(), "等待超时");
    }
}
