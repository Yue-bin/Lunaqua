using System.Net;
using System.Text.Json;
using Lunaqua.Infrastructure.OpenList;
using Xunit;

namespace Lunaqua.Tests;

public sealed class OpenListClientTests
{
    [Fact]
    public async Task 站点设置能解析()
    {
        using var http = FakeHttp.Create((_, _) => FakeHttp.Envelope(new Dictionary<string, string>
        {
            ["version"] = "v4.2.2",
            ["search_index"] = "none",
            ["default_page_size"] = "30",
        }));

        var settings = await new OpenListClient(http).GetSiteSettingsAsync(TestContext.Current.CancellationToken);

        Assert.Equal("v4.2.2", settings.Version);
        Assert.False(settings.SearchAvailable);
        Assert.Equal(30, settings.DefaultPageSize);
    }

    [Fact]
    public async Task 列目录会显式传per_page并解析条目()
    {
        string? sentBody = null;
        using var http = FakeHttp.Create((_, body) =>
        {
            sentBody = body;
            return FakeHttp.Envelope(new
            {
                content = new object[] { FakeHttp.Dir("stick.plugins.demo"), FakeHttp.File("readme.md", 2952, "https://x/raw") },
                total = 2,
                readme = string.Empty,
                write = true,
                provider = "Local",
            });
        });

        var result = await new OpenListClient(http).ListAsync("/", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(sentBody);
        using var document = JsonDocument.Parse(sentBody!);
        Assert.Equal(100, document.RootElement.GetProperty("per_page").GetInt32());
        Assert.Equal("/", document.RootElement.GetProperty("path").GetString());

        Assert.Equal(2, result.Entries.Count);
        Assert.True(result.Entries[0].IsDirectory);
        Assert.Equal("readme.md", result.Entries[1].Name);
        Assert.True(result.Writable);
    }

    [Fact]
    public async Task 业务错误码抛OpenListException()
    {
        using var http = FakeHttp.Create((_, _) => FakeHttp.Envelope(null, 500, "object not found"));

        var exception = await Assert.ThrowsAsync<OpenListException>(() => new OpenListClient(http).GetFileAsync("/nope/info.json", TestContext.Current.CancellationToken));

        Assert.Equal(500, exception.Code);
        Assert.True(exception.IsNotFound);
        Assert.Contains("object not found", exception.Message);
    }

    [Fact]
    public async Task 返回前端页面时抛异常而不是当成成功()
    {
        using var http = FakeHttp.Create((_, _) => FakeHttp.Text("<!doctype html><html><body>SPA</body></html>", "text/html"));

        var exception = await Assert.ThrowsAsync<OpenListException>(() => new OpenListClient(http).ListAsync("/", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(0, exception.Code);
        Assert.Contains("不是 OpenList JSON", exception.Message);
    }

    [Fact]
    public async Task 直链下载用真HTTP状态码报错()
    {
        using var http = FakeHttp.Create((_, _) => FakeHttp.Status(HttpStatusCode.Unauthorized, "sign invalid"));

        var exception = await Assert.ThrowsAsync<OpenListException>(async () =>
        {
            using var stream = new MemoryStream();
            await new OpenListClient(http).DownloadAsync("https://x/d/file.dll", stream, cancellationToken: TestContext.Current.CancellationToken);
        });

        Assert.Equal(401, exception.Code);
    }

    [Fact]
    public async Task 读文本走fs_get再GET直链()
    {
        var calls = new List<string>();
        using var http = FakeHttp.Create((request, _) =>
        {
            calls.Add(request.RequestUri!.ToString());

            if (request.Method == HttpMethod.Post)
            {
                return FakeHttp.Envelope(FakeHttp.File("info.json", 20, "https://x/raw/info.json"));
            }

            return FakeHttp.Text("{\"schema\":1}");
        });

        var text = await new OpenListClient(http).ReadTextAsync("/demo/info.json", TestContext.Current.CancellationToken);

        Assert.Equal("{\"schema\":1}", text);
        Assert.Equal(2, calls.Count);
        Assert.Equal("https://x/raw/info.json", calls[1]);
    }

    [Fact]
    public async Task 五xx会重试()
    {
        var attempts = 0;
        using var http = FakeHttp.Create((_, _) =>
        {
            attempts++;
            return attempts < 3
                ? FakeHttp.Status(HttpStatusCode.BadGateway, "bad gateway")
                : FakeHttp.Envelope(new { content = Array.Empty<object>(), total = 0, write = false });
        });

        var client = new OpenListClient(http, new OpenListOptions { RetryCount = 2 });
        var result = await client.ListAsync("/", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, attempts);
        Assert.Empty(result.Entries);
    }
}
