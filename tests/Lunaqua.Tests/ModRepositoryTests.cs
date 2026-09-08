using System.Text.Json;
using Lunaqua.Domain;
using Lunaqua.Infrastructure.OpenList;
using Lunaqua.Services;
using Xunit;

namespace Lunaqua.Tests;

public sealed class ModRepositoryTests
{
    [Fact]
    public async Task 扫描目录并解析info_json()
    {
        var demoJson = TestMetadata.Json(id: "stick.plugins.demo", name: "演示插件", latest: "1.0.0");
        var otherJson = TestMetadata.Json(id: "real.id", name: "名字对不上", latest: "2.0.0");

        using var http = FakeHttp.Create((request, body) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var payload = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body).RootElement;
            var target = payload.TryGetProperty("path", out var p) ? p.GetString() ?? string.Empty : string.Empty;

            if (path.EndsWith("/api/fs/list", StringComparison.Ordinal))
            {
                return FakeHttp.Envelope(new
                {
                    content = new object[] { FakeHttp.Dir("stick.plugins.demo"), FakeHttp.Dir("other.dir"), FakeHttp.Dir("BepInEx") },
                    total = 3,
                    write = false,
                });
            }

            if (path.EndsWith("/api/fs/get", StringComparison.Ordinal))
            {
                return target switch
                {
                    "/stick.plugins.demo/info.json" => FakeHttp.Envelope(FakeHttp.File("info.json", 100, "https://x/raw/demo")),
                    "/other.dir/info.json" => FakeHttp.Envelope(FakeHttp.File("info.json", 100, "https://x/raw/other")),
                    _ => FakeHttp.Envelope(null, 500, "object not found"),
                };
            }

            return target switch
            {
                _ => FakeHttp.Text("https://x/raw/demo" == request.RequestUri.ToString() ? demoJson : otherJson),
            };
        });

        var repository = new ModRepository(new OpenListClient(http));
        var catalog = await repository.LoadCatalogAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, catalog.ModCount);
        Assert.Empty(catalog.Issues);

        var demo = catalog.Entries.Single(entry => entry.DirectoryName == "stick.plugins.demo");
        Assert.False(demo.IdMismatch);
        Assert.Equal("演示插件", demo.Info.Name);

        var mismatched = catalog.Entries.Single(entry => entry.DirectoryName == "other.dir");
        Assert.True(mismatched.IdMismatch);
        Assert.Contains("不一致", mismatched.WarningText);
    }

    [Fact]
    public async Task 没有info_json的目录被跳过且不算问题()
    {
        using var http = FakeHttp.Create((request, _) =>
            request.RequestUri!.AbsolutePath.EndsWith("/api/fs/list", StringComparison.Ordinal)
                ? FakeHttp.Envelope(new { content = new object[] { FakeHttp.Dir("BepInEx") }, total = 1, write = false })
                : FakeHttp.Envelope(null, 500, "object not found"));

        var catalog = await new ModRepository(new OpenListClient(http)).LoadCatalogAsync(TestContext.Current.CancellationToken);

        Assert.Empty(catalog.Entries);
        Assert.Empty(catalog.Issues);
    }

    [Fact]
    public async Task info_json校验失败进Issues()
    {
        using var http = FakeHttp.Create((request, body) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/api/fs/list", StringComparison.Ordinal))
            {
                return FakeHttp.Envelope(new { content = new object[] { FakeHttp.Dir("broken.mod") }, total = 1, write = false });
            }

            if (path.EndsWith("/api/fs/get", StringComparison.Ordinal))
            {
                return FakeHttp.Envelope(FakeHttp.File("info.json", 10, "https://x/raw/broken"));
            }

            return FakeHttp.Text("{ 这不是 JSON");
        });

        var catalog = await new ModRepository(new OpenListClient(http)).LoadCatalogAsync(TestContext.Current.CancellationToken);

        Assert.Empty(catalog.Entries);
        var issue = Assert.Single(catalog.Issues);
        Assert.Equal("broken.mod", issue.DirectoryName);
        Assert.NotEmpty(issue.Errors);
    }

    [Fact]
    public async Task readme缺失返回null()
    {
        using var http = FakeHttp.Create((_, _) => FakeHttp.Envelope(null, 500, "object not found"));

        var readme = await new ModRepository(new OpenListClient(http)).ReadReadmeAsync("demo", TestContext.Current.CancellationToken);

        Assert.Null(readme);
    }
}
