using System.Diagnostics;
using Lunaqua.Domain;
using Serilog;

namespace Lunaqua.Services;

/// <summary>启动游戏并等待 BepInEx 日志的结果。</summary>
/// <param name="Success">日志里出现了预期的版本标记。</param>
/// <param name="LogText">读到的日志（可能为空）。</param>
/// <param name="Message">给玩家看的中文说明。</param>
public sealed record GameLaunchResult(bool Success, string? LogText, string Message);

/// <summary>启动游戏、等 BepInEx 日志、再收尸。</summary>
public interface IGameLauncher
{
    Task<GameLaunchResult> LaunchAndWaitForBepInExLogAsync(
        GameContext game,
        string logMarker,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>真实实现：直接跑 exe（不必经 Steam），轮询 <c>BepInEx/LogOutput.log</c>。</summary>
public sealed class ProcessGameLauncher : IGameLauncher
{
    private readonly ILogger _log;

    public ProcessGameLauncher(ILogger? log = null) => _log = log ?? Log.ForContext<ProcessGameLauncher>();

    public async Task<GameLaunchResult> LaunchAndWaitForBepInExLogAsync(
        GameContext game,
        string logMarker,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var executable = FindExecutable(game.GameDirectory);
        if (executable is null)
        {
            return new GameLaunchResult(false, null, "没找到 StickFight.exe，没法验证。");
        }

        var logPath = Path.Combine(game.GameDirectory, "BepInEx", "LogOutput.log");
        TryDelete(logPath);   // 别读到上一次的日志

        using var process = Process.Start(new ProcessStartInfo(executable)
        {
            WorkingDirectory = game.GameDirectory,
            UseShellExecute = false,
        });

        if (process is null)
        {
            return new GameLaunchResult(false, null, "游戏进程没起来。");
        }

        _log.Information("已启动游戏验证：{Executable}（{Pid}）", executable, process.Id);

        try
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var text = TryRead(logPath);
                if (text is not null && text.Contains(logMarker, StringComparison.Ordinal))
                {
                    return new GameLaunchResult(true, text, "BepInEx 已在游戏里跑起来。");
                }

                if (process.HasExited)
                {
                    // 进程退了，再给日志一点落盘时间
                    await Task.Delay(500, cancellationToken);
                    text = TryRead(logPath);
                    return text is not null && text.Contains(logMarker, StringComparison.Ordinal)
                        ? new GameLaunchResult(true, text, "BepInEx 已在游戏里跑起来。")
                        : new GameLaunchResult(false, text, "游戏起来了但没看到 BepInEx 日志，可能被杀软拦了。");
                }

                await Task.Delay(500, cancellationToken);
            }

            var partial = TryRead(logPath);
            return new GameLaunchResult(false, partial, $"等了 {timeout.TotalSeconds:0} 秒也没看到 BepInEx 日志。");
        }
        finally
        {
            TryKill(process);
        }
    }

    private static string? FindExecutable(string gameDirectory)
    {
        foreach (var name in GameInstallationValidator.ExecutableNames)
        {
            var candidate = Path.Combine(gameDirectory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private string? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 删不掉就让 BepInEx 自己覆盖
        }
    }

    private void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
                _log.Information("已结束游戏进程 {Pid}", process.Id);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _log.Warning(ex, "结束游戏进程失败");
        }
    }
}
