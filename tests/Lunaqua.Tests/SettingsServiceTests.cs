using Lunaqua.Models;
using Lunaqua.Services;
using Xunit;

namespace Lunaqua.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly string _file;

    public SettingsServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "lunaqua-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _file = Path.Combine(_directory, "settings.json");
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void 文件不存在时使用默认设置()
    {
        var service = new SettingsService(_file);

        service.Load();

        Assert.Equal(AppTheme.System, service.Current.Theme);
        Assert.True(service.Current.CheckUpdateOnStartup);
        Assert.Null(service.Current.GameDirectory);
    }

    [Fact]
    public void 保存后能原样读回()
    {
        var service = new SettingsService(_file);
        service.Load();

        service.Update(s =>
        {
            s.GameDirectory = @"D:\Steam\steamapps\common\StickFight";
            s.Theme = AppTheme.Dark;
            s.CheckUpdateOnStartup = false;
        });

        var reloaded = new SettingsService(_file);
        reloaded.Load();

        Assert.Equal(@"D:\Steam\steamapps\common\StickFight", reloaded.Current.GameDirectory);
        Assert.Equal(AppTheme.Dark, reloaded.Current.Theme);
        Assert.False(reloaded.Current.CheckUpdateOnStartup);
    }

    [Fact]
    public void 修改会触发Changed事件()
    {
        var service = new SettingsService(_file);
        service.Load();

        var raised = 0;
        service.Changed += (_, _) => raised++;

        service.Update(s => s.Theme = AppTheme.Light);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void 文件损坏时回退默认且不抛异常()
    {
        File.WriteAllText(_file, "{ 这不是合法 JSON");

        var service = new SettingsService(_file);

        service.Load();

        Assert.Equal(AppTheme.System, service.Current.Theme);
    }
}
