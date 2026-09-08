using Avalonia;
using Lunaqua.Services;
using Serilog;

namespace Lunaqua;

/// <summary>应用入口：先起日志，再起 Avalonia。</summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        AppPaths.EnsureCreated();
        LogSetup.Initialize();

        try
        {
            Log.Information("Lunaqua 启动（数据目录：{Root}）", AppPaths.Root);
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            Log.Information("Lunaqua 正常退出");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Lunaqua 启动失败");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>供 Avalonia 设计器与无头测试复用。</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
