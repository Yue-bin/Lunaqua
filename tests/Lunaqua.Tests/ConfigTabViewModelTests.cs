using Lunaqua.Domain;
using Lunaqua.Infrastructure.BepInEx;
using Lunaqua.Services;
using Lunaqua.ViewModels;
using Xunit;

namespace Lunaqua.Tests;

public sealed class ConfigTabViewModelTests : IDisposable
{
    private const string Cfg = "## Plugin GUID: stick.plugins.demo\r\n" +
        "\r\n" +
        "[General]\r\n" +
        "\r\n" +
        "## 是否屏蔽踢出\r\n" +
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
        "## 日志级别\r\n" +
        "# Setting type: LogLevel\r\n" +
        "# Default value: Info\r\n" +
        "# Acceptable values: Debug, Info, Warning, Error\r\n" +
        "LogLevel = Warning\r\n" +
        "\r\n" +
        "## 快捷键\r\n" +
        "# Setting type: KeyboardShortcut\r\n" +
        "# Default value: F2\r\n" +
        "MenuKey = LeftControl + K\r\n";

    private readonly string _root;
    private readonly GameContext _game;
    private readonly SettingsService _settings;
    private readonly string _cfgPath;

    public ConfigTabViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "lunaqua-configtab", Guid.NewGuid().ToString("N"));
        _game = new GameContext(Path.Combine(_root, "game"));
        Directory.CreateDirectory(ConfigEditorService.ConfigDirectory(_game));
        _cfgPath = Path.Combine(ConfigEditorService.ConfigDirectory(_game), "stick.plugins.demo.cfg");
        File.WriteAllText(_cfgPath, Cfg);

        _settings = new SettingsService(Path.Combine(_root, "settings.json"));
        _settings.Load();
        _settings.Update(settings => settings.GameDirectory = _game.GameDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void 载入后按类型生成控件种类()
    {
        var tab = CreateTab();
        tab.Load("stick.plugins.demo", "stick.plugins.demo");

        Assert.True(tab.HasConfig);
        Assert.Equal(4, tab.Entries.Count);

        var blockKicks = tab.Entries.Single(entry => entry.Key == "BlockKicks");
        Assert.True(blockKicks.IsBoolean);
        Assert.True(blockKicks.BoolValue);

        var guiScale = tab.Entries.Single(entry => entry.Key == "GuiScale");
        Assert.True(guiScale.IsNumber);
        Assert.Equal(0.5m, guiScale.Minimum);
        Assert.Equal(2m, guiScale.Maximum);
        Assert.Equal(1.22m, guiScale.NumberValue);
        Assert.True(guiScale.IsChanged);

        var logLevel = tab.Entries.Single(entry => entry.Key == "LogLevel");
        Assert.True(logLevel.IsEnum);
        Assert.Equal("Warning", logLevel.SelectedOption);
        Assert.Equal(4, logLevel.Options.Count);

        var menuKey = tab.Entries.Single(entry => entry.Key == "MenuKey");
        Assert.True(menuKey.IsText);
        Assert.Equal("LeftControl + K", menuKey.Text);
    }

    [Fact]
    public void 改布尔值即改即存并备份()
    {
        var tab = CreateTab();
        tab.Load("stick.plugins.demo", "stick.plugins.demo");
        var entry = tab.Entries.Single(item => item.Key == "BlockKicks");

        entry.BoolValue = false;

        Assert.Contains("BlockKicks = false", File.ReadAllText(_cfgPath));
        Assert.Contains("## 是否屏蔽踢出", File.ReadAllText(_cfgPath));
        var backups = Directory.GetFiles(Path.Combine(_game.LunaquaDirectory, "backup", "config", "stick.plugins.demo"), "*.cfg");
        Assert.Single(backups);
        Assert.Contains("BlockKicks = true", File.ReadAllText(backups[0]));
    }

    [Fact]
    public void 非法值被拒绝且文件不变()
    {
        var tab = CreateTab();
        tab.Load("stick.plugins.demo", "stick.plugins.demo");
        var entry = tab.Entries.Single(item => item.Key == "GuiScale");

        entry.Text = "abc";
        entry.CommitTextCommand.Execute(null);

        Assert.True(entry.HasError);
        Assert.Contains("数字", entry.ErrorText);
        Assert.Contains("GuiScale = 1.22", File.ReadAllText(_cfgPath));
    }

    [Fact]
    public void 超出范围的值被拒绝()
    {
        var tab = CreateTab();
        tab.Load("stick.plugins.demo", "stick.plugins.demo");
        var entry = tab.Entries.Single(item => item.Key == "GuiScale");

        entry.NumberValue = 3m;

        Assert.True(entry.HasError);
        Assert.Contains("不能大于", entry.ErrorText);
    }

    [Fact]
    public void 只看已改动项能过滤()
    {
        var tab = CreateTab();
        tab.Load("stick.plugins.demo", "stick.plugins.demo");
        Assert.Equal(4, tab.Entries.Count);

        tab.OnlyChanged = true;

        Assert.Equal(3, tab.Entries.Count);   // GuiScale / LogLevel / MenuKey 与默认值不同
        Assert.All(tab.Entries, entry => Assert.True(entry.IsChanged));
    }

    [Fact]
    public void 全部重置恢复默认值()
    {
        var tab = CreateTab();
        tab.Load("stick.plugins.demo", "stick.plugins.demo");

        tab.ResetAllCommand.Execute(null);

        Assert.All(tab.Entries, entry => Assert.False(entry.IsChanged));
        var text = File.ReadAllText(_cfgPath);
        Assert.Contains("GuiScale = 1", text);
        Assert.Contains("LogLevel = Info", text);
    }

    [Fact]
    public void 游戏运行中只读()
    {
        var tab = CreateTab(gameRunning: true);
        tab.Load("stick.plugins.demo", "stick.plugins.demo");

        Assert.True(tab.IsReadOnly);
        Assert.All(tab.Entries, entry => Assert.False(entry.IsEditable));

        var entry = tab.Entries.Single(item => item.Key == "BlockKicks");
        entry.BoolValue = false;

        Assert.Contains("BlockKicks = true", File.ReadAllText(_cfgPath));
        Assert.Contains("只读", tab.StatusText);
    }

    [Fact]
    public void 没有配置文件时提示先启动游戏()
    {
        File.Delete(_cfgPath);
        var tab = CreateTab();

        tab.Load("stick.plugins.demo", "stick.plugins.demo");

        Assert.True(tab.HasNoConfig);
        Assert.Contains("先启动一次游戏", tab.StatusText);
    }

    [Fact]
    public void 能改真实playermanager配置且注释不丢()
    {
        var source = Environment.GetEnvironmentVariable("LUNAQUA_TEST_CFG");
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
        {
            Assert.Skip("没设 LUNAQUA_TEST_CFG，跳过真实配置联调");
            return;
        }

        var original = File.ReadAllText(source);
        File.Copy(source, _cfgPath, overwrite: true);

        var tab = CreateTab();
        tab.Load(CfgFile.Parse(original, source).PluginGuid, "stick.plugins.playermanager");

        Assert.True(tab.HasConfig);
        var guiScale = tab.Entries.FirstOrDefault(entry => entry.Key == "GuiScale");
        if (guiScale is null)
        {
            Assert.Skip("真实配置里没有 GuiScale，跳过");
            return;
        }

        var target = guiScale.NumberValue >= 1.5m ? 1m : 1.5m;
        guiScale.NumberValue = target;

        var text = File.ReadAllText(_cfgPath);
        Assert.Contains($"GuiScale = {target.ToString(System.Globalization.CultureInfo.InvariantCulture)}", text);
        Assert.Contains("##", text);
        Assert.Equal(
            original.Split('\n').Count(line => line.TrimStart().StartsWith("##", StringComparison.Ordinal)),
            text.Split('\n').Count(line => line.TrimStart().StartsWith("##", StringComparison.Ordinal)));
    }

    private ConfigTabViewModel CreateTab(bool gameRunning = false)
    {
        var detector = new FakeProcessDetector { Running = gameRunning };
        var service = new ConfigEditorService(detector);
        return new ConfigTabViewModel(service, detector, _settings);
    }
}
