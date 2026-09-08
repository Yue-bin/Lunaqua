namespace Lunaqua.Services;

/// <summary>
/// 应用数据目录（默认 %LocalAppData%\Lunaqua）。
/// 环境变量 LUNAQUA_DATA_DIR 可覆盖，用于测试与便携场景。
/// </summary>
public static class AppPaths
{
    private static readonly string RootPath = ResolveRoot();

    public static string Root => RootPath;

    public static string Logs => Path.Combine(RootPath, "logs");

    public static string Cache => Path.Combine(RootPath, "cache");

    public static string Backups => Path.Combine(RootPath, "backups");

    public static string SettingsFile => Path.Combine(RootPath, "settings.json");

    /// <summary>创建应用需要的目录（幂等）。</summary>
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Cache);
        Directory.CreateDirectory(Backups);
    }

    private static string ResolveRoot()
    {
        var custom = Environment.GetEnvironmentVariable("LUNAQUA_DATA_DIR");
        return string.IsNullOrWhiteSpace(custom)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lunaqua")
            : Path.GetFullPath(custom);
    }
}
