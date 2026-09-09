using System.Text.Json;
using System.Text.Json.Serialization;
using Lunaqua.Domain;
using Serilog;

namespace Lunaqua.Services;

/// <summary>读取随程序发布的官方构建指纹表。</summary>
public static class GameFingerprintTableLoader
{
    /// <summary>嵌入资源名（见 Lunaqua.csproj 的 EmbeddedResource）。</summary>
    public const string ResourceName = "Lunaqua.data.game-fingerprints.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static GameFingerprintTable Load(ILogger? log = null)
    {
        log ??= Log.ForContext(typeof(GameFingerprintTableLoader));

        try
        {
            using var stream = typeof(GameFingerprintTableLoader).Assembly.GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                log.Warning("没找到嵌入的指纹表资源 {Resource}", ResourceName);
                return GameFingerprintTable.Empty;
            }

            using var reader = new StreamReader(stream);
            var table = JsonSerializer.Deserialize<GameFingerprintTable>(reader.ReadToEnd(), Options);
            if (table is null)
            {
                return GameFingerprintTable.Empty;
            }

            log.Information("指纹表载入：{Count} 个官方构建", table.Count);
            return table;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            log.Error(ex, "指纹表解析失败，按空表处理");
            return GameFingerprintTable.Empty;
        }
    }

    /// <summary>从 JSON 文本解析（测试用）。</summary>
    public static GameFingerprintTable Parse(string json) =>
        JsonSerializer.Deserialize<GameFingerprintTable>(json, Options) ?? GameFingerprintTable.Empty;
}
