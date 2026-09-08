using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lunaqua.Infrastructure.OpenList;

/// <summary>OpenList 统一响应包装。</summary>
public sealed class OpenListEnvelope<T>
{
    [JsonPropertyName("code")] public int Code { get; set; }

    [JsonPropertyName("message")] public string? Message { get; set; }

    [JsonPropertyName("data")] public T? Data { get; set; }
}

/// <summary>fs/list 与 fs/get 的条目。</summary>
public sealed class FsEntryDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("size")] public long Size { get; set; }

    [JsonPropertyName("is_dir")] public bool IsDirectory { get; set; }

    [JsonPropertyName("modified")] public DateTimeOffset? Modified { get; set; }

    [JsonPropertyName("created")] public DateTimeOffset? Created { get; set; }

    [JsonPropertyName("sign")] public string? Sign { get; set; }

    [JsonPropertyName("type")] public int Type { get; set; }

    [JsonPropertyName("raw_url")] public string? RawUrl { get; set; }
}

/// <summary>fs/list 的 data。</summary>
public sealed class FsListDataDto
{
    [JsonPropertyName("content")] public List<FsEntryDto>? Content { get; set; }

    [JsonPropertyName("total")] public int Total { get; set; }

    [JsonPropertyName("readme")] public string? Readme { get; set; }

    [JsonPropertyName("write")] public bool Write { get; set; }

    [JsonPropertyName("provider")] public string? Provider { get; set; }
}

/// <summary>站点公开设置（键值全是字符串）。</summary>
public sealed record OpenListSiteSettings(string Version, string SearchIndex, int DefaultPageSize)
{
    /// <summary>站点是否启用了搜索索引（本站为 none）。</summary>
    public bool SearchAvailable => !string.Equals(SearchIndex, "none", StringComparison.OrdinalIgnoreCase);

    public static OpenListSiteSettings FromDictionary(IReadOnlyDictionary<string, JsonElement> values) =>
        new(
            GetString(values, "version"),
            GetString(values, "search_index"),
            GetInt(values, "default_page_size", 30));

    private static string GetString(IReadOnlyDictionary<string, JsonElement> values, string key) =>
        values.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : string.Empty;

    private static int GetInt(IReadOnlyDictionary<string, JsonElement> values, string key, int fallback)
    {
        if (!values.TryGetValue(key, out var element))
        {
            return fallback;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(element.GetString(), out var number) => number,
            _ => fallback,
        };
    }
}

/// <summary>目录项（给上层用的形状）。</summary>
public sealed record OpenListEntry(string Name, long Size, bool IsDirectory, DateTimeOffset? Modified, string? Sign, int Type);

/// <summary>列目录结果。</summary>
public sealed record OpenListListResult(IReadOnlyList<OpenListEntry> Entries, int Total, bool Writable, string? Provider);

/// <summary>单个文件信息（含带签名的直链）。</summary>
public sealed record OpenListFileInfo(string Name, long Size, bool IsDirectory, string RawUrl, DateTimeOffset? Modified, string? Sign);
