using Lunaqua.Domain;
using Xunit;

namespace Lunaqua.Tests;

public sealed class SemVerTests
{
    [Theory]
    [InlineData("1.0.0", 1, 0, 0, "", "")]
    [InlineData("5.1.0", 5, 1, 0, "", "")]
    [InlineData("1.0.0-rc.1", 1, 0, 0, "rc.1", "")]
    [InlineData("0.0.1-alpha", 0, 0, 1, "alpha", "")]
    [InlineData("1.2.3+build.5", 1, 2, 3, "", "build.5")]
    [InlineData("1.2.3-rc.1+sha.abc", 1, 2, 3, "rc.1", "sha.abc")]
    public void 能解析合法版本号(string text, int major, int minor, int patch, string prerelease, string build)
    {
        Assert.True(SemVer.TryParse(text, out var version));
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(prerelease, version.Prerelease);
        Assert.Equal(build, version.BuildMetadata);
        Assert.Equal(text, version.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("v1.0.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0-")]
    [InlineData("1.0.0-rc..1")]
    [InlineData("1.0.0-rc.01")]
    [InlineData("1.0.0+")]
    [InlineData("1.0.0-alpha_beta")]
    [InlineData("最新版")]
    public void 拒绝非法版本号(string? text)
    {
        Assert.False(SemVer.TryParse(text, out _));
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.2.0", "1.10.0", -1)]
    [InlineData("2.0.0", "1.9.9", 1)]
    [InlineData("1.0.0-rc.1", "1.0.0", -1)]
    [InlineData("1.0.0-rc.1", "1.0.0-rc.2", -1)]
    [InlineData("1.0.0-rc.10", "1.0.0-rc.9", 1)]
    [InlineData("1.0.0-alpha", "1.0.0-1", 1)]
    [InlineData("1.0.0", "1.0.0+build.1", 0)]
    public void 按SemVer规则比较(string left, string right, int expected)
    {
        Assert.True(SemVer.TryParse(left, out var a));
        Assert.True(SemVer.TryParse(right, out var b));

        Assert.Equal(expected, Math.Sign(a.CompareTo(b)));
    }

    [Fact]
    public void 忽略构建元数据做相等判断()
    {
        Assert.True(SemVer.TryParse("1.0.0+a", out var a));
        Assert.True(SemVer.TryParse("1.0.0+b", out var b));

        Assert.Equal(a, b);
    }
}
