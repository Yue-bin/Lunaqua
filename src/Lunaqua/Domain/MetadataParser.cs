using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lunaqua.Domain;

/// <summary>
/// info.json 解析与校验（规格书 §4.2）。
/// 策略：致命字段缺失/非法 → 不收录；文案字段缺失 → 警告但仍然收录（站上存量数据不全）。
/// </summary>
public static class MetadataParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <param name="json">info.json 原文。</param>
    /// <param name="directoryName">站上目录名（用于比对 id）。</param>
    public static MetadataParseResult Parse(string json, string directoryName)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        InfoDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<InfoDto>(json, Options);
        }
        catch (JsonException ex)
        {
            return new MetadataParseResult(null, [$"info.json 不是合法 JSON：{ex.Message}"], warnings);
        }

        if (dto is null)
        {
            return new MetadataParseResult(null, ["info.json 内容为空。"], warnings);
        }

        if (dto.Schema is not 1)
        {
            errors.Add($"schema 必须是 1，实际为 {(dto.Schema?.ToString() ?? "缺失")}。");
        }

        var id = dto.Id?.Trim() ?? string.Empty;
        if (id.Length == 0)
        {
            errors.Add("缺少 id。");
        }
        else if (!string.Equals(id, directoryName, StringComparison.Ordinal))
        {
            warnings.Add($"目录名「{directoryName}」与 id「{id}」不一致，按 id 记账。");
        }

        var type = dto.Type?.Trim() ?? string.Empty;
        if (!ModTypes.IsKnown(type))
        {
            errors.Add($"type 必须是 bepinex / uvfs / uvfs-framework，实际为「{dto.Type}」。");
        }

        var guid = dto.Guid?.Trim();
        if (type is ModTypes.BepInEx or ModTypes.UvfsFramework && string.IsNullOrWhiteSpace(guid))
        {
            errors.Add($"{type} 类型必须带 guid。");
        }

        var name = dto.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            errors.Add("缺少 name。");
        }

        var author = dto.Author?.Trim() ?? string.Empty;
        if (author.Length == 0)
        {
            warnings.Add("没有作者信息，界面显示「未知作者」。");
        }

        var description = dto.Description?.Trim() ?? string.Empty;
        if (description.Length == 0)
        {
            warnings.Add("没有简介。");
        }

        var dependencies = ParseDependencies(dto.Dependencies, errors);
        var versions = ParseVersions(dto.Versions, errors, warnings);

        SemVer? latest = null;
        var latestText = dto.Latest?.Trim() ?? string.Empty;
        if (latestText.Length == 0)
        {
            errors.Add("缺少 latest。");
        }
        else if (!SemVer.TryParse(latestText, out latest))
        {
            errors.Add($"latest「{latestText}」不是合法 SemVer。");
        }
        else if (!versions.Any(v => v.Version.Equals(latest)))
        {
            errors.Add($"latest「{latest}」不在 versions 里。");
        }

        if (errors.Count > 0)
        {
            return new MetadataParseResult(null, errors, warnings);
        }

        var info = new MetadataInfo(
            dto.Schema!.Value,
            id,
            type,
            guid,
            name,
            author,
            description,
            latest!,
            dependencies,
            versions);

        return new MetadataParseResult(info, [], warnings);
    }

    private static IReadOnlyList<MetadataDependency> ParseDependencies(List<DependencyDto>? items, List<string> errors)
    {
        var result = new List<MetadataDependency>();
        if (items is null)
        {
            return result;
        }

        foreach (var item in items)
        {
            var id = item.Id?.Trim() ?? string.Empty;
            if (id.Length == 0)
            {
                errors.Add("dependencies 里有空 id。");
                continue;
            }

            SemVer? minVersion = null;
            var minText = item.MinVersion?.Trim();
            if (!string.IsNullOrEmpty(minText))
            {
                if (SemVer.TryParse(minText, out var parsed))
                {
                    minVersion = parsed;
                }
                else
                {
                    errors.Add($"依赖 {id} 的 minVersion「{minText}」不是合法 SemVer。");
                    continue;
                }
            }

            result.Add(new MetadataDependency(id, minVersion));
        }

        return result;
    }

    private static IReadOnlyList<ModVersion> ParseVersions(List<VersionDto>? items, List<string> errors, List<string> warnings)
    {
        var result = new List<ModVersion>();
        if (items is null || items.Count == 0)
        {
            errors.Add("versions 为空。");
            return result;
        }

        foreach (var item in items)
        {
            var text = item.Version?.Trim() ?? string.Empty;
            if (!SemVer.TryParse(text, out var version))
            {
                errors.Add($"versions 里的「{item.Version}」不是合法 SemVer。");
                continue;
            }

            if (result.Any(v => v.Version.Equals(version)))
            {
                warnings.Add($"版本 {version} 重复。");
            }

            var released = item.Released?.Trim() ?? string.Empty;
            if (released.Length == 0)
            {
                warnings.Add($"版本 {version} 缺少 released。");
            }
            else if (!DateOnly.TryParse(released, out _))
            {
                warnings.Add($"版本 {version} 的 released「{released}」不是 ISO 日期。");
            }

            var files = ParseFiles(version, item.Files, errors);
            if (files.Count == 0)
            {
                errors.Add($"版本 {version} 没有文件清单。");
            }

            result.Add(new ModVersion(version, released, files, item.Changelog?.Trim()));
        }

        return result;
    }

    private static IReadOnlyList<MetadataFile> ParseFiles(SemVer version, List<FileDto>? items, List<string> errors)
    {
        var result = new List<MetadataFile>();
        if (items is null)
        {
            return result;
        }

        foreach (var item in items)
        {
            var path = item.Path?.Trim() ?? string.Empty;
            if (!IsSafeRelativePath(path))
            {
                errors.Add($"版本 {version} 的文件路径「{item.Path}」不安全（不允许绝对路径、.. 或反斜杠）。");
                continue;
            }

            var sha256 = item.Sha256?.Trim() ?? string.Empty;
            if (!IsLowerHexSha256(sha256))
            {
                errors.Add($"版本 {version} 的文件「{path}」sha256 不是 64 位小写十六进制。");
                continue;
            }

            if (item.Size is null or < 0)
            {
                errors.Add($"版本 {version} 的文件「{path}」size 非法。");
                continue;
            }

            result.Add(new MetadataFile(path, sha256, item.Size.Value));
        }

        return result;
    }

    /// <summary>拒绝绝对路径、路径穿越与反斜杠，防止落到目标目录之外。</summary>
    public static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.Contains(':'))
        {
            return false;
        }

        if (path.StartsWith('/'))
        {
            return false;
        }

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                return false;
            }

            if (segment.Any(char.IsControl))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLowerHexSha256(string value) =>
        value.Length == 64 && value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private sealed class InfoDto
    {
        [JsonPropertyName("schema")] public int? Schema { get; set; }

        [JsonPropertyName("id")] public string? Id { get; set; }

        [JsonPropertyName("type")] public string? Type { get; set; }

        [JsonPropertyName("guid")] public string? Guid { get; set; }

        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("author")] public string? Author { get; set; }

        [JsonPropertyName("description")] public string? Description { get; set; }

        [JsonPropertyName("latest")] public string? Latest { get; set; }

        [JsonPropertyName("dependencies")] public List<DependencyDto>? Dependencies { get; set; }

        [JsonPropertyName("versions")] public List<VersionDto>? Versions { get; set; }
    }

    private sealed class DependencyDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }

        [JsonPropertyName("minVersion")] public string? MinVersion { get; set; }
    }

    private sealed class VersionDto
    {
        [JsonPropertyName("version")] public string? Version { get; set; }

        [JsonPropertyName("released")] public string? Released { get; set; }

        [JsonPropertyName("files")] public List<FileDto>? Files { get; set; }

        [JsonPropertyName("changelog")] public string? Changelog { get; set; }
    }

    private sealed class FileDto
    {
        [JsonPropertyName("path")] public string? Path { get; set; }

        [JsonPropertyName("sha256")] public string? Sha256 { get; set; }

        [JsonPropertyName("size")] public long? Size { get; set; }
    }
}
