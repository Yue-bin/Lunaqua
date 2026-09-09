using Lunaqua.Domain;
using Lunaqua.Infrastructure.BepInEx;
using Lunaqua.Services;
using Xunit;

namespace Lunaqua.Tests;

public sealed class CfgFileTests
{
    private const string Sample = "## Settings file was created by plugin PlayerManager v5.1.0\r\n" +
        "## Plugin GUID: stick.plugins.playermanager\r\n" +
        "\r\n" +
        "[General]\r\n" +
        "\r\n" +
        "## 是否屏蔽来自其他玩家的踢出请求\r\n" +
        "# Setting type: Boolean\r\n" +
        "# Default value: true\r\n" +
        "BlockKicks = true\r\n" +
        "\r\n" +
        "## 界面缩放\r\n" +
        "# Setting type: Single\r\n" +
        "# Default value: 1\r\n" +
        "# Acceptable value range: From 0.5 to 2\r\n" +
        "GuiScale = 1.22\r\n" +
        "\r\n" +
        "## 打开界面的按键\r\n" +
        "# Setting type: KeyboardShortcut\r\n" +
        "# Default value: F2\r\n" +
        "MenuKey = LeftControl + K\r\n" +
        "\r\n" +
        "## 日志级别\r\n" +
        "# Setting type: LogLevel\r\n" +
        "# Default value: Info\r\n" +
        "# Acceptable values: Debug, Info, Warning, Error\r\n" +
        "LogLevel = Warning\r\n";

    [Fact]
    public void 能解析注释类型默认值与范围()
    {
        var file = CfgFile.Parse(Sample);

        Assert.Equal("stick.plugins.playermanager", file.PluginGuid);
        Assert.Equal(4, file.Entries.Count);

        var blockKicks = file.Entries[0];
        Assert.Equal("General", blockKicks.Section);
        Assert.Equal("BlockKicks", blockKicks.Key);
        Assert.Equal(ConfigValueKind.Boolean, blockKicks.Kind);
        Assert.Equal("true", blockKicks.DefaultValue);
        Assert.Equal("是否屏蔽来自其他玩家的踢出请求", blockKicks.Description);
        Assert.False(blockKicks.IsChanged);

        var guiScale = file.Entries[1];
        Assert.Equal(ConfigValueKind.Single, guiScale.Kind);
        Assert.Equal(0.5, guiScale.Minimum);
        Assert.Equal(2, guiScale.Maximum);

        var menuKey = file.Entries[2];
        Assert.Equal(ConfigValueKind.KeyboardShortcut, menuKey.Kind);
        Assert.Equal("LeftControl + K", menuKey.Value);

        var logLevel = file.Entries[3];
        Assert.Equal(ConfigValueKind.Enum, logLevel.Kind);
        Assert.Equal(["Debug", "Info", "Warning", "Error"], logLevel.AcceptableValues);
        Assert.True(logLevel.IsChanged);
    }

    [Fact]
    public void 原样回写不改一个字节()
    {
        var file = CfgFile.Parse(Sample);

        Assert.Equal(Sample, file.Serialize());
    }

    [Fact]
    public void 改值只动值行注释与顺序不变()
    {
        var file = CfgFile.Parse(Sample);
        var guiScale = file.Entries.Single(entry => entry.Key == "GuiScale");

        Assert.True(file.SetValue(guiScale, "1.5"));

        var text = file.Serialize();
        Assert.Contains("GuiScale = 1.5", text);
        Assert.Contains("## 界面缩放", text);
        Assert.Contains("# Acceptable value range: From 0.5 to 2", text);
        Assert.Contains("BlockKicks = true", text);
        Assert.Contains("\r\n", text);

        // 只有一行变了
        var before = Sample.Replace("\r\n", "\n").Split('\n');
        var after = text.Replace("\r\n", "\n").Split('\n');
        Assert.Equal(before.Length, after.Length);
        var changed = before.Zip(after).Count(pair => pair.First != pair.Second);
        Assert.Equal(1, changed);

        // 重新解析拿到新值
        Assert.Equal("1.5", CfgFile.Parse(text).Entries.Single(entry => entry.Key == "GuiScale").Value);
    }

    [Fact]
    public void 重置恢复默认值()
    {
        var file = CfgFile.Parse(Sample);
        var logLevel = file.Entries.Single(entry => entry.Key == "LogLevel");

        file.Reset(logLevel);

        Assert.Equal("Info", logLevel.Value);
        Assert.False(logLevel.IsChanged);
        Assert.Contains("LogLevel = Info", file.Serialize());
    }

    [Fact]
    public void 没有插件GUID也能解析()
    {
        var file = CfgFile.Parse("""
            [General]
            Value = 1
            """);

        Assert.Null(file.PluginGuid);
        Assert.Single(file.Entries);
        Assert.Equal("Value", file.Entries[0].Key);
    }

    [Fact]
    public void 真实配置文件能原样回写()
    {
        var path = Environment.GetEnvironmentVariable("LUNAQUA_TEST_CFG");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Assert.Skip("没设 LUNAQUA_TEST_CFG，跳过真实配置文件回环");
            return;
        }

        var original = File.ReadAllText(path);
        var file = CfgFile.Parse(original, path);

        Assert.Equal(original, file.Serialize());
        Assert.NotEmpty(file.Entries);
    }
}

public sealed class ConfigValueValidatorTests
{
    private static CfgEntry Entry(ConfigValueKind kind, string? defaultValue = null, string[]? acceptable = null, double? min = null, double? max = null) =>
        new("General", "Key", string.Empty, null, kind.ToString(), kind, defaultValue, acceptable ?? [], min, max, false, 0);

    [Theory]
    [InlineData("true", "true", true)]
    [InlineData("TRUE", "true", true)]
    [InlineData("False", "false", true)]
    [InlineData("yes", "yes", false)]
    public void 布尔校验(string input, string expected, bool ok)
    {
        var result = ConfigValueValidator.Validate(Entry(ConfigValueKind.Boolean), input);

        Assert.Equal(ok, result.Ok);
        if (ok)
        {
            Assert.Equal(expected, result.Value);
        }
    }

    [Fact]
    public void 整数与范围校验()
    {
        var entry = Entry(ConfigValueKind.Integer, "100", min: 0, max: 100000);

        Assert.True(ConfigValueValidator.Validate(entry, "50").Ok);
        Assert.False(ConfigValueValidator.Validate(entry, "50.5").Ok);
        Assert.False(ConfigValueValidator.Validate(entry, "-1").Ok);
        Assert.False(ConfigValueValidator.Validate(entry, "100001").Ok);
    }

    [Fact]
    public void 小数用不变文化解析()
    {
        var entry = Entry(ConfigValueKind.Single, "1", min: 0.5, max: 2);

        Assert.True(ConfigValueValidator.Validate(entry, "1.5").Ok);
        Assert.False(ConfigValueValidator.Validate(entry, "1,5").Ok);
        Assert.False(ConfigValueValidator.Validate(entry, "2.5").Ok);
    }

    [Fact]
    public void 枚举与多值校验()
    {
        var enumEntry = Entry(ConfigValueKind.Enum, "Info", ["Debug", "Info", "Warning", "Error"]);
        Assert.True(ConfigValueValidator.Validate(enumEntry, "Warning").Ok);
        Assert.False(ConfigValueValidator.Validate(enumEntry, "Verbose").Ok);

        var flags = Entry(ConfigValueKind.Flags, null, ["A", "B", "C"]);
        Assert.True(ConfigValueValidator.Validate(flags, "A, C").Ok);
        Assert.Equal("A, C", ConfigValueValidator.Validate(flags, "A,C").Value);
        Assert.False(ConfigValueValidator.Validate(flags, "A, D").Ok);
    }

    [Fact]
    public void 快捷键校验与规范化()
    {
        var entry = Entry(ConfigValueKind.KeyboardShortcut);

        Assert.Equal("LeftControl + K", ConfigValueValidator.Validate(entry, "K + LeftControl").Value);
        Assert.Equal("Not set", ConfigValueValidator.Validate(entry, "").Value);
        Assert.True(ConfigValueValidator.Validate(entry, "F8").Ok);
        Assert.False(ConfigValueValidator.Validate(entry, "Ctrl + K").Ok);
    }

    [Fact]
    public void 颜色校验与规范化()
    {
        var entry = Entry(ConfigValueKind.Color);

        Assert.Equal("FFD700FF", ConfigValueValidator.Validate(entry, "FFD700FF").Value);
        Assert.Equal("FFD700FF", ConfigValueValidator.Validate(entry, "#ffd700").Value);
        Assert.False(ConfigValueValidator.Validate(entry, "红色").Ok);
        Assert.False(ConfigValueValidator.Validate(entry, "FFD70").Ok);
    }
}

public sealed class ConfigEditorServiceTests : IDisposable
{
    private readonly string _root;
    private readonly GameContext _game;

    public ConfigEditorServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lunaqua-cfg", Guid.NewGuid().ToString("N"));
        _game = new GameContext(Path.Combine(_root, "game"));
        Directory.CreateDirectory(ConfigEditorService.ConfigDirectory(_game));

        File.WriteAllText(
            Path.Combine(ConfigEditorService.ConfigDirectory(_game), "stick.plugins.demo.cfg"),
            "## Plugin GUID: stick.plugins.demo\n\n[General]\n\n# Setting type: Boolean\n# Default value: false\nEnabled = false\n");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void 能按GUID找到配置()
    {
        var service = new ConfigEditorService(new FakeProcessDetector());

        var file = service.FindForGuid(_game, "stick.plugins.demo");

        Assert.NotNull(file);
        Assert.Single(file!.Entries);
        Assert.Null(service.FindForGuid(_game, "不存在的.guid"));
    }

    [Fact]
    public void 写回前先备份()
    {
        var service = new ConfigEditorService(new FakeProcessDetector());
        var file = service.FindForGuid(_game, "stick.plugins.demo")!;
        file.SetValue(file.Entries[0], "true");

        service.Save(_game, file);

        var backups = Directory.GetFiles(Path.Combine(_game.LunaquaDirectory, "backup", "config", "stick.plugins.demo"), "*.cfg");
        Assert.Single(backups);
        Assert.Contains("Enabled = false", File.ReadAllText(backups[0]));
        Assert.Contains("Enabled = true", File.ReadAllText(file.Path));
    }

    [Fact]
    public void 游戏运行中拒绝写回()
    {
        var service = new ConfigEditorService(new FakeProcessDetector { Running = true });
        var file = service.FindForGuid(_game, "stick.plugins.demo")!;

        Assert.True(service.IsGameRunning);
        Assert.Throws<InvalidOperationException>(() => service.Save(_game, file));
    }
}
