using Lunaqua.Domain;
using Lunaqua.Infrastructure;
using Lunaqua.Infrastructure.Steam;
using Xunit;

namespace Lunaqua.Tests;

public sealed class KeyValuesTests
{
    [Fact]
    public void 能解析嵌套与注释()
    {
        const string Text = """
            // 这是注释
            "libraryfolders"
            {
                "0"
                {
                    "path"		"C:\\Program Files (x86)\\Steam"
                    "apps"
                    {
                        "674940"		"123456"
                    }
                }
                "1"
                {
                    "path"		"D:\\SteamLibrary"
                }
            }
            """;

        var root = KeyValuesParser.Parse(Text).Child("libraryfolders");

        Assert.NotNull(root);
        Assert.Equal(2, root!.Entries().Count);

        var first = root.Child("0");
        Assert.NotNull(first);
        Assert.Equal(@"C:\Program Files (x86)\Steam", first!.GetString("path"));
        Assert.Equal("123456", first.Child("apps")!.GetString("674940"));
    }
}

public sealed class AppManifestTests
{
    [Fact]
    public void 能解析安装目录与buildid()
    {
        const string Text = """
            "AppState"
            {
                "appid"		"674940"
                "name"		"Stick Fight: The Game"
                "installdir"		"StickFight"
                "buildid"		"16543210"
                "StateFlags"		"4"
            }
            """;

        var manifest = AppManifest.Parse(Text);

        Assert.NotNull(manifest);
        Assert.Equal("674940", manifest!.AppId);
        Assert.Equal("StickFight", manifest.InstallDir);
        Assert.Equal("16543210", manifest.BuildId);
        Assert.True(manifest.IsFullyInstalled);
    }

    [Fact]
    public void 缺installdir返回null()
    {
        Assert.Null(AppManifest.Parse("""
            "AppState"
            {
                "appid"		"674940"
            }
            """));
    }
}

public sealed class SteamLibraryLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunaqua-steam", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void 能按libraryfolders找到游戏目录()
    {
        var steam = Path.Combine(_root, "Steam");
        var library = Path.Combine(_root, "Library");
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        Directory.CreateDirectory(Path.Combine(library, "steamapps", "common", "StickFight"));

        File.WriteAllText(
            Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
            $$"""
              "libraryfolders"
              {
                  "0"
                  {
                      "path"		"{{steam.Replace("\\", "\\\\")}}"
                  }
                  "1"
                  {
                      "path"		"{{library.Replace("\\", "\\\\")}}"
                  }
              }
              """);

        File.WriteAllText(
            Path.Combine(library, "steamapps", "appmanifest_674940.acf"),
            """
            "AppState"
            {
                "appid"		"674940"
                "installdir"		"StickFight"
                "buildid"		"16543210"
                "StateFlags"		"4"
            }
            """);

        var locator = new SteamLibraryLocator(steamPathProvider: () => steam);
        var libraries = locator.ReadLibraries(steam);

        Assert.Equal(2, libraries.Count);

        var game = locator.FindGameDirectory("674940");
        Assert.Equal(Path.Combine(library, "steamapps", "common", "StickFight"), game);
    }

    [Fact]
    public void 没装游戏时返回null()
    {
        var steam = Path.Combine(_root, "Steam");
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        File.WriteAllText(
            Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
            $$"""
              "libraryfolders"
              {
                  "0"
                  {
                      "path"		"{{steam.Replace("\\", "\\\\")}}"
                  }
              }
              """);

        var locator = new SteamLibraryLocator(steamPathProvider: () => steam);

        Assert.Null(locator.FindGameDirectory("674940"));
    }
}

public sealed class PeImageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunaqua-pe", Guid.NewGuid().ToString("N"));

    public PeImageTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void 能识别64位与32位()
    {
        var x64 = Path.Combine(_root, "x64.exe");
        var x86 = Path.Combine(_root, "x86.exe");
        File.WriteAllBytes(x64, TestPe.Create(TestPe.MachineAmd64));
        File.WriteAllBytes(x86, TestPe.Create(TestPe.MachineI386));

        Assert.Equal(PeArchitecture.X64, PeImage.ReadArchitecture(x64));
        Assert.Equal(PeArchitecture.X86, PeImage.ReadArchitecture(x86));
        Assert.True(PeImage.IsX64(x64));
        Assert.False(PeImage.IsX64(x86));
    }

    [Fact]
    public void 非PE文件返回Unknown()
    {
        var path = Path.Combine(_root, "text.txt");
        File.WriteAllText(path, "这不是可执行文件");

        Assert.Equal(PeArchitecture.Unknown, PeImage.ReadArchitecture(path));
        Assert.Equal(PeArchitecture.Unknown, PeImage.ReadArchitecture(Path.Combine(_root, "不存在.exe")));
    }
}

public sealed class GameInstallationValidatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunaqua-game", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void 结构完整的64位目录通过()
    {
        var game = Path.Combine(_root, "StickFight");
        Directory.CreateDirectory(Path.Combine(game, "StickFight_Data", "Managed"));
        File.WriteAllBytes(Path.Combine(game, "StickFight.exe"), TestPe.Create(TestPe.MachineAmd64));
        File.WriteAllText(Path.Combine(game, "StickFight_Data", "Managed", "Assembly-CSharp.dll"), "fake");

        var result = GameInstallationValidator.Validate(game);

        Assert.True(result.IsValid, result.Summary);
        Assert.True(result.IsX64);
        Assert.NotNull(result.ExecutablePath);
        Assert.NotNull(result.ManagedAssemblyPath);
    }

    [Fact]
    public void 缺Managed目录时给出问题()
    {
        var game = Path.Combine(_root, "Broken");
        Directory.CreateDirectory(game);
        File.WriteAllBytes(Path.Combine(game, "StickFight.exe"), TestPe.Create(TestPe.MachineAmd64));

        var result = GameInstallationValidator.Validate(game);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Contains("Assembly-CSharp.dll"));
    }

    [Fact]
    public void 三十二位游戏被拒()
    {
        var game = Path.Combine(_root, "OldGame");
        Directory.CreateDirectory(Path.Combine(game, "StickFight_Data", "Managed"));
        File.WriteAllBytes(Path.Combine(game, "StickFight.exe"), TestPe.Create(TestPe.MachineI386));
        File.WriteAllText(Path.Combine(game, "StickFight_Data", "Managed", "Assembly-CSharp.dll"), "fake");

        var result = GameInstallationValidator.Validate(game);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Contains("64 位"));
    }

    [Fact]
    public void 目录不存在时报错()
    {
        var result = GameInstallationValidator.Validate(Path.Combine(_root, "不存在"));

        Assert.False(result.IsValid);
        Assert.Contains("目录不存在", result.Summary);
    }
}
