using Lunaqua.Domain;
using Xunit;

namespace Lunaqua.Tests;

public sealed class MetadataParserTests
{
    [Fact]
    public void 合法元数据能解析()
    {
        var result = MetadataParser.Parse(TestMetadata.Json(versions: ["1.1.0", "1.0.0"], latest: "1.1.0"), "stick.plugins.demo");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Info);

        var info = result.Info!;
        Assert.Equal("stick.plugins.demo", info.Id);
        Assert.Equal(ModTypes.BepInEx, info.Type);
        Assert.Equal("1.1.0", info.Latest.ToString());
        Assert.Equal(2, info.Versions.Count);
        Assert.Equal("1.1.0", info.LatestVersion.Version.ToString());
        Assert.Single(info.Versions[0].Files);
    }

    [Fact]
    public void 目录名与id不一致时给警告但仍然收录()
    {
        var result = MetadataParser.Parse(TestMetadata.Json(id: "stick.plugins.demo"), "stick.plugins.other");

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("不一致"));
    }

    [Fact]
    public void 缺作者与简介只给警告()
    {
        var result = MetadataParser.Parse(TestMetadata.Json(author: "", description: ""), "stick.plugins.demo");

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("作者"));
        Assert.Contains(result.Warnings, w => w.Contains("简介"));
    }

    [Fact]
    public void latest不在版本列表里时报错()
    {
        var result = MetadataParser.Parse(TestMetadata.Json(latest: "2.0.0", versions: ["1.0.0"]), "stick.plugins.demo");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("latest"));
    }

    [Theory]
    [InlineData("../evil.dll")]
    [InlineData("/etc/passwd")]
    [InlineData("sub\\..\\evil.dll")]
    [InlineData("C:/windows/system32/evil.dll")]
    [InlineData("")]
    public void 文件路径不安全时报错(string path)
    {
        var result = MetadataParser.Parse(TestMetadata.Json(filePath: path), "stick.plugins.demo");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("路径"));
    }

    [Fact]
    public void sha256格式不对时报错()
    {
        var result = MetadataParser.Parse(TestMetadata.Json(sha256: "ABCDEF"), "stick.plugins.demo");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("sha256"));
    }

    [Fact]
    public void 未知类型时报错()
    {
        var result = MetadataParser.Parse(TestMetadata.Json(type: "exe"), "stick.plugins.demo");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("type"));
    }

    [Fact]
    public void bepinex缺guid时报错()
    {
        var result = MetadataParser.Parse(TestMetadata.Json(guid: null), "stick.plugins.demo");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("guid"));
    }

    [Fact]
    public void uvfs不要求guid()
    {
        var result = MetadataParser.Parse(TestMetadata.Json(type: "uvfs", guid: null), "stick.plugins.demo");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void schema不是1时报错()
    {
        var result = MetadataParser.Parse(TestMetadata.Json(schema: 2), "stick.plugins.demo");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("schema"));
    }

    [Fact]
    public void 版本号不是SemVer时报错()
    {
        var result = MetadataParser.Parse(TestMetadata.Json(latest: "最新版", versions: ["最新版"]), "stick.plugins.demo");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("SemVer"));
    }

    [Fact]
    public void 不是JSON时报错()
    {
        var result = MetadataParser.Parse("<html>SPA</html>", "stick.plugins.demo");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("JSON"));
    }

    [Fact]
    public void 预发布版本能解析()
    {
        var result = MetadataParser.Parse(TestMetadata.Json(latest: "1.0.0-rc.1", versions: ["1.0.0-rc.1", "0.9.0"]), "stick.plugins.demo");

        Assert.True(result.IsValid);
        Assert.True(result.Info!.Latest.IsPrerelease);
    }
}
