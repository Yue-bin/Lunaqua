using System.Diagnostics;
using Serilog;

namespace Lunaqua.Services;

/// <summary>游戏是否正在运行（运行中禁止安装/卸载/启停）。</summary>
public interface IGameProcessDetector
{
    bool IsGameRunning();
}

/// <inheritdoc />
public sealed class GameProcessDetector : IGameProcessDetector
{
    /// <summary>可能的进程名（64 位更新后名字未实机核对，两个都查）。</summary>
    public static readonly string[] ProcessNames = ["StickFight", "StickFightTheGame"];

    private readonly ILogger _log;

    public GameProcessDetector(ILogger? log = null) => _log = log ?? Log.ForContext<GameProcessDetector>();

    public bool IsGameRunning()
    {
        foreach (var name in ProcessNames)
        {
            try
            {
                if (Process.GetProcessesByName(name).Length > 0)
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
            {
                _log.Warning(ex, "查询进程 {Name} 失败", name);
            }
        }

        return false;
    }
}
