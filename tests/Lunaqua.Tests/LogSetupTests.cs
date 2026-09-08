using Lunaqua.Services;
using Serilog;
using Xunit;

namespace Lunaqua.Tests;

public sealed class LogSetupTests
{
    [Fact]
    public void 初始化后日志能落文件()
    {
        LogSetup.Initialize();

        const string Marker = "lunaqua-m0-自检";
        Log.Information("M0 日志落盘自检 {Marker}", Marker);
        Log.CloseAndFlush();

        var files = Directory.GetFiles(AppPaths.Logs, "lunaqua-*.log");
        Assert.NotEmpty(files);

        var latest = files.OrderByDescending(File.GetLastWriteTimeUtc).First();
        Assert.Contains(Marker, File.ReadAllText(latest));
    }
}
