using Avalonia;
using Lunaqua.Services;
using Serilog;
using Velopack;

namespace Lunaqua;

/// <summary>应用入口：先起日志，再起 Avalonia。</summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack 的钩子必须最先跑（处理 --veloapp-* 参数；开发运行时时是空操作）
        VelopackApp.Build().Run();

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
