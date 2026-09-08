using Serilog;
using Serilog.Events;

namespace Lunaqua.Services;

/// <summary>Serilog 文件日志：按天滚动、保留 14 天，玩家可在设置页导出。</summary>
public static class LogSetup
{
    public static void Initialize(LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        Directory.CreateDirectory(AppPaths.Logs);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.File(
                Path.Combine(AppPaths.Logs, "lunaqua-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
