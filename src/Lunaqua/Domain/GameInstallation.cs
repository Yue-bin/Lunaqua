namespace Lunaqua.Domain;

/// <summary>一次游戏目录校验的结果。</summary>
/// <param name="GameDirectory">目录。</param>
/// <param name="ExecutablePath">找到的可执行文件；没找到为 null。</param>
/// <param name="ManagedAssemblyPath">Assembly-CSharp.dll 路径；没找到为 null。</param>
/// <param name="IsX64">可执行文件是否 64 位。</param>
/// <param name="Issues">问题清单（空表示通过）。</param>
public sealed record GameInstallation(
    string GameDirectory,
    string? ExecutablePath,
    string? ManagedAssemblyPath,
    bool IsX64,
    IReadOnlyList<string> Issues)
{
    public bool IsValid => Issues.Count == 0;

    public string Summary => IsValid ? "目录看起来没问题。" : string.Join("；", Issues);
}

/// <summary>游戏目录结构校验（规格书 §8 第 1 步）。</summary>
public static class GameInstallationValidator
{
    /// <summary>可能的可执行文件名（64 位更新后命名未实机核对，两个都认）。</summary>
    public static readonly string[] ExecutableNames = ["StickFight.exe", "StickFightTheGame.exe"];

    public static GameInstallation Validate(string gameDirectory)
    {
        var issues = new List<string>();
        string? executable = null;
        string? managed = null;

        if (!Directory.Exists(gameDirectory))
        {
            return new GameInstallation(gameDirectory, null, null, false, ["目录不存在。"]);
        }

        foreach (var name in ExecutableNames)
        {
            var candidate = Path.Combine(gameDirectory, name);
            if (File.Exists(candidate))
            {
                executable = candidate;
                break;
            }
        }

        if (executable is null)
        {
            issues.Add("目录里没找到 StickFight.exe（或 StickFightTheGame.exe）。");
        }

        if (executable is not null)
        {
            var dataDirectory = Path.Combine(gameDirectory, Path.GetFileNameWithoutExtension(executable) + "_Data");
            managed = Path.Combine(dataDirectory, "Managed", "Assembly-CSharp.dll");
            if (!File.Exists(managed))
            {
                issues.Add("没找到 <exe>_Data\\Managed\\Assembly-CSharp.dll，游戏可能没装完。");
                managed = null;
            }
        }

        var isX64 = executable is not null && Infrastructure.PeImage.IsX64(executable);
        if (executable is not null && !isX64)
        {
            issues.Add("游戏不是 64 位版本，请先在 Steam 里更新游戏。");
        }

        return new GameInstallation(gameDirectory, executable, managed, isX64, issues);
    }
}
