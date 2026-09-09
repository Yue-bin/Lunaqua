namespace Lunaqua.Infrastructure.Steam;

/// <summary>appmanifest_&lt;AppID&gt;.acf 的关键字段。</summary>
/// <param name="AppId">AppID。</param>
/// <param name="Name">游戏名。</param>
/// <param name="InstallDir">安装目录名（相对 steamapps/common）。</param>
/// <param name="BuildId">构建号（干净判定的第一层）。</param>
/// <param name="StateFlags">状态位；4 = 已完整安装。</param>
public sealed record AppManifest(string AppId, string Name, string InstallDir, string BuildId, int StateFlags)
{
    /// <summary>StateFlags 的第 2 位表示「已完整安装」。</summary>
    public bool IsFullyInstalled => (StateFlags & 4) != 0;

    public static AppManifest? Parse(string acfText)
    {
        var root = KeyValuesParser.Parse(acfText).Child("AppState");
        if (root is null)
        {
            return null;
        }

        var installDir = root.GetString("installdir");
        if (string.IsNullOrWhiteSpace(installDir))
        {
            return null;
        }

        _ = int.TryParse(root.GetString("StateFlags"), out var stateFlags);

        return new AppManifest(
            root.GetString("appid") ?? string.Empty,
            root.GetString("name") ?? string.Empty,
            installDir!,
            root.GetString("buildid") ?? string.Empty,
            stateFlags);
    }
}
