using System.Security.Cryptography;
using Lunaqua.Domain;
using Xunit;

namespace Lunaqua.Tests;

/// <summary>测试用的元数据构造器（直接造对象，不走 JSON）。</summary>
internal static class TestMod
{
    public static SemVer Version(string text)
    {
        Assert.True(SemVer.TryParse(text, out var version), $"测试数据里的版本号非法：{text}");
        return version!;
    }

    public static byte[] Bytes(string content) => System.Text.Encoding.UTF8.GetBytes(content);

    public static string Sha256(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    public static MetadataFile File(string path, byte[] content) => new(path, Sha256(content), content.Length);

    public static ModVersion Release(string version, params (string Path, byte[] Content)[] files)
    {
        var list = files.Select(item => File(item.Path, item.Content)).ToList();
        return new ModVersion(Version(version), "2026-09-09", list, "测试版本");
    }

    /// <summary>把元数据序列化成 info.json 文本（用于假站点）。</summary>
    public static string ToJson(MetadataInfo info) => System.Text.Json.JsonSerializer.Serialize(new
    {
        schema = info.Schema,
        id = info.Id,
        type = info.Type,
        guid = info.Guid,
        name = info.Name,
        author = info.Author,
        description = info.Description,
        latest = info.Latest.ToString(),
        dependencies = info.Dependencies.Select(dependency => new { id = dependency.Id, minVersion = dependency.MinVersion?.ToString() }),
        versions = info.Versions.Select(version => new
        {
            version = version.Version.ToString(),
            released = version.Released,
            files = version.Files.Select(file => new { path = file.Path, sha256 = file.Sha256, size = file.Size }),
            changelog = version.Changelog,
        }),
    });

    public static MetadataInfo Info(
        string id = "stick.plugins.demo",
        string type = ModTypes.BepInEx,
        string? guid = "stick.plugins.demo",
        string name = "演示插件",
        string author = "某人",
        string description = "演示用。",
        params ModVersion[] versions)
    {
        var list = versions.Length > 0 ? versions : [Release("1.0.0", ("demo.dll", Bytes("demo")))];
        var latest = list.MaxBy(item => item.Version)!;

        return new MetadataInfo(1, id, type, guid, name, author, description, latest.Version, [], list);
    }
}

/// <summary>可控的游戏运行检测（测试用）。</summary>
internal sealed class FakeProcessDetector : Lunaqua.Services.IGameProcessDetector
{
    public bool Running { get; set; }

    public bool IsGameRunning() => Running;
}
