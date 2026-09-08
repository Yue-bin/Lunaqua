using System.Net;
using System.Text;
using System.Text.Json;

namespace Lunaqua.Tests;

/// <summary>把请求交给委托处理的 HttpClient 工厂（不打网络）。</summary>
internal static class FakeHttp
{
    public static HttpClient Create(Func<HttpRequestMessage, string, HttpResponseMessage> respond) =>
        new(new DelegateHandler(respond)) { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>OpenList 成功响应。</summary>
    public static HttpResponseMessage Envelope(object? data, int code = 200, string message = "success") =>
        Json(new { code, message, data });

    public static HttpResponseMessage Json(object? payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };

    public static HttpResponseMessage Text(string text, string mediaType = "text/plain") =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(text, Encoding.UTF8, mediaType),
        };

    public static HttpResponseMessage Status(HttpStatusCode status, string body = "") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "text/html") };

    /// <summary>目录项 JSON（OpenList 的字段名是 snake_case）。</summary>
    public static object Dir(string name) => new
    {
        name,
        size = 0L,
        is_dir = true,
        modified = "2026-09-09T02:57:18+08:00",
        created = "2026-02-27T02:40:41+08:00",
        sign = string.Empty,
        thumb = string.Empty,
        type = 1,
        hashinfo = "null",
        hash_info = (object?)null,
    };

    public static object File(string name, long size, string rawUrl) => new
    {
        name,
        size,
        is_dir = false,
        modified = "2026-09-09T06:53:30+08:00",
        created = "2026-09-09T06:53:30+08:00",
        sign = "signature:0",
        thumb = string.Empty,
        type = 4,
        hashinfo = "null",
        hash_info = (object?)null,
        raw_url = rawUrl,
        readme = string.Empty,
        header = string.Empty,
        provider = "Local",
        related = (object?)null,
    };

    private sealed class DelegateHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request, body);
        }
    }
}
