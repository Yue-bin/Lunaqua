using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lunaqua.Tests;

/// <summary>测试用的 info.json 构造器。</summary>
internal static class TestMetadata
{
    public static string Json(
        string id = "stick.plugins.demo",
        string type = "bepinex",
        string? guid = "stick.plugins.demo",
        string name = "演示插件",
        string author = "某人",
        string description = "演示用。",
        string latest = "1.0.0",
        string[]? versions = null,
        string[]? dependencies = null,
        string filePath = "demo.dll",
        string sha256 = "9f2c000000000000000000000000000000000000000000000000000000000000",
        long size = 1024,
        int schema = 1)
    {
        var versionArray = new JsonArray();
        foreach (var version in versions ?? [latest])
        {
            versionArray.Add(new JsonObject
            {
                ["version"] = version,
                ["released"] = "2026-08-31",
                ["files"] = new JsonArray(new JsonObject
                {
                    ["path"] = filePath,
                    ["sha256"] = sha256,
                    ["size"] = size,
                }),
                ["changelog"] = "首个版本",
            });
        }

        var dependencyArray = new JsonArray();
        foreach (var dependency in dependencies ?? [])
        {
            dependencyArray.Add(new JsonObject { ["id"] = dependency });
        }

        var root = new JsonObject
        {
            ["schema"] = schema,
            ["id"] = id,
            ["type"] = type,
            ["name"] = name,
            ["author"] = author,
            ["description"] = description,
            ["latest"] = latest,
            ["dependencies"] = dependencyArray,
            ["versions"] = versionArray,
        };

        if (guid is not null)
        {
            root["guid"] = guid;
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
