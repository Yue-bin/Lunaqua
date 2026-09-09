using System.Text.Json;

namespace Lunaqua.Tests;

/// <summary>假站点：按 <c>/&lt;目录&gt;/&lt;文件&gt;</c> 提供 fs/get 与直链下载。</summary>
internal sealed class FakeModSite
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public FakeModSite Add(string path, byte[] content)
    {
        _files[path] = content;
        return this;
    }

    /// <summary>收到过的请求数（用于验证缓存命中不重复下载）。</summary>
    public int Requests { get; private set; }

    public HttpClient CreateClient() => FakeHttp.Create((request, body) =>
    {
        Requests++;
        var path = request.RequestUri!.AbsolutePath;

        if (path.EndsWith("/api/fs/list", StringComparison.Ordinal))
        {
            var directories = _files.Keys
                .Select(key => key.TrimStart('/').Split('/')[0])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            return FakeHttp.Envelope(new
            {
                content = directories.Select(FakeHttp.Dir).ToArray(),
                total = directories.Length,
                write = false,
            });
        }

        if (path.EndsWith("/api/fs/get", StringComparison.Ordinal))
        {
            var target = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body).RootElement;
            var file = target.TryGetProperty("path", out var value) ? value.GetString() ?? string.Empty : string.Empty;

            return _files.ContainsKey(file)
                ? FakeHttp.Envelope(FakeHttp.File(Path.GetFileName(file), _files[file].Length, "https://fake/raw" + file))
                : FakeHttp.Envelope(null, 500, "object not found");
        }

        var requested = request.RequestUri.ToString().Replace("https://fake/raw", string.Empty, StringComparison.Ordinal);
        return _files.TryGetValue(requested, out var content)
            ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(content) }
            : FakeHttp.Status(System.Net.HttpStatusCode.NotFound, "not found");
    });
}
