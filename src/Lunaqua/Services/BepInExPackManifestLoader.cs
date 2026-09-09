using System.Text.Json;
using Lunaqua.Domain;
using Serilog;

namespace Lunaqua.Services;

/// <summary>读取随程序发布的 BepInEx pack 清单。</summary>
public static class BepInExPackManifestLoader
{
    public const string ResourceName = "Lunaqua.data.bepinex-pack.json";

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static BepInExPackManifest Load(ILogger? log = null)
    {
        log ??= Log.ForContext(typeof(BepInExPackManifestLoader));

        try
        {
            using var stream = typeof(BepInExPackManifestLoader).Assembly.GetManifestResourceStream(ResourceName);
            if (stream is null)
            {
                log.Warning("没找到嵌入的 pack 清单 {Resource}", ResourceName);
                return BepInExPackManifest.Empty;
            }

            using var reader = new StreamReader(stream);
            var manifest = JsonSerializer.Deserialize<BepInExPackManifest>(reader.ReadToEnd(), Options);
            log.Information("BepInEx pack 清单载入：{Version}，{Count} 个文件", manifest?.Version, manifest?.FileCount);
            return manifest ?? BepInExPackManifest.Empty;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            log.Error(ex, "pack 清单解析失败");
            return BepInExPackManifest.Empty;
        }
    }
}
