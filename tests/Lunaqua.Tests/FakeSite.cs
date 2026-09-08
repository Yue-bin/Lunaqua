using System.Text.Json;

namespace Lunaqua.Tests;

/// <summary>一个假的 OpenList 站点：目录列表 + 每个目录的 info.json / readme.md。</summary>
internal static class FakeSite
{
    public static HttpClient Create(IReadOnlyDictionary<string, string> filesByDirectory, bool withReadme = true)
    {
        var directories = filesByDirectory.Keys.ToList();

        return FakeHttp.Create((request, body) =>
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/api/fs/list", StringComparison.Ordinal))
            {
                return FakeHttp.Envelope(new
                {
                    content = directories.Select(FakeHttp.Dir).ToArray(),
                    total = directories.Count,
                    write = false,
                });
            }

            if (path.EndsWith("/api/fs/get", StringComparison.Ordinal))
            {
                var target = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body).RootElement;
                var file = target.TryGetProperty("path", out var value) ? value.GetString() ?? string.Empty : string.Empty;

                var known = filesByDirectory.Keys.Any(directory => file == $"/{directory}/info.json")
                    || (withReadme && filesByDirectory.Keys.Any(directory => file == $"/{directory}/readme.md"));

                return known
                    ? FakeHttp.Envelope(FakeHttp.File(Path.GetFileName(file), 100, "https://fake/raw" + file))
                    : FakeHttp.Envelope(null, 500, "object not found");
            }

            var requested = request.RequestUri.ToString().Replace("https://fake/raw", string.Empty, StringComparison.Ordinal);
            foreach (var (directory, json) in filesByDirectory)
            {
                if (requested == $"/{directory}/info.json")
                {
                    return FakeHttp.Text(json);
                }

                if (withReadme && requested == $"/{directory}/readme.md")
                {
                    return FakeHttp.Text($"# {directory}\n\n这是 {directory} 的说明。");
                }
            }

            return FakeHttp.Status(System.Net.HttpStatusCode.NotFound, "not found");
        });
    }
}
