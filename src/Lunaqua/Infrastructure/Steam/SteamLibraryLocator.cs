using Microsoft.Win32;
using Serilog;

namespace Lunaqua.Infrastructure.Steam;

/// <summary>一个 Steam 库目录与其中的 AppID。</summary>
/// <param name="Path">库根目录。</param>
/// <param name="AppIds">该库里的 AppID。</param>
public sealed record SteamLibrary(string Path, IReadOnlyList<string> AppIds)
{
    public string SteamAppsDirectory => System.IO.Path.Combine(Path, "steamapps");

    public string CommonDirectory => System.IO.Path.Combine(SteamAppsDirectory, "common");
}

/// <summary>定位 Steam 与游戏目录（规格书 §8 第 1 步）。</summary>
public sealed class SteamLibraryLocator
{
    private readonly ILogger _log;
    private readonly Func<string?> _steamPathProvider;

    public SteamLibraryLocator(ILogger? log = null, Func<string?>? steamPathProvider = null)
    {
        _log = log ?? Log.ForContext<SteamLibraryLocator>();
        _steamPathProvider = steamPathProvider ?? ReadSteamPathFromRegistry;
    }

    /// <summary>从注册表读 Steam 安装路径（HKCU 优先，HKLM 兜底）。</summary>
    public static string? ReadSteamPathFromRegistry()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var currentUser = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
            if (currentUser?.GetValue("SteamPath") is string fromUser && fromUser.Length > 0)
            {
                return fromUser.Replace('/', Path.DirectorySeparatorChar);
            }

            using var localMachine = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");

            if (localMachine?.GetValue("InstallPath") is string fromMachine && fromMachine.Length > 0)
            {
                return fromMachine;
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Serilog.Log.Debug(ex, "读取 Steam 注册表路径失败");
        }

        return null;
    }

    public string? FindSteamPath()
    {
        var path = _steamPathProvider();
        return string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) ? null : path;
    }

    /// <summary>解析 libraryfolders.vdf，列出全部库。</summary>
    public IReadOnlyList<SteamLibrary> ReadLibraries(string steamPath)
    {
        var vdfPath = FindLibraryFoldersFile(steamPath);
        if (vdfPath is null)
        {
            _log.Warning("没找到 libraryfolders.vdf：{Path}", steamPath);
            return [];
        }

        var root = KeyValuesParser.Parse(File.ReadAllText(vdfPath)).Child("libraryfolders");
        if (root is null)
        {
            return [];
        }

        var libraries = new List<SteamLibrary>();
        foreach (var (_, node) in root.Entries())
        {
            var path = node.GetString("path");
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            path = path.Replace("\\\\", "\\");
            var apps = node.Child("apps")?.Entries().Select(entry => entry.Key).ToList() ?? [];
            libraries.Add(new SteamLibrary(path!, apps));
        }

        return libraries;
    }

    /// <summary>找到的游戏位置（目录 + appmanifest）。</summary>
    /// <param name="Directory">游戏根目录。</param>
    /// <param name="Manifest">appmanifest 的关键字段（buildid 用于干净判定第一层）。</param>
    public sealed record GameLocation(string Directory, AppManifest Manifest);

    /// <summary>在各库里找 <c>appmanifest_&lt;appId&gt;.acf</c>，返回游戏目录与 manifest。</summary>
    public GameLocation? FindGame(string appId, string? steamPath = null)
    {
        steamPath ??= FindSteamPath();
        if (string.IsNullOrWhiteSpace(steamPath))
        {
            _log.Information("没找到 Steam 安装路径");
            return null;
        }

        foreach (var library in ReadLibraries(steamPath))
        {
            var manifestPath = Path.Combine(library.SteamAppsDirectory, $"appmanifest_{appId}.acf");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var manifest = AppManifest.Parse(File.ReadAllText(manifestPath));
            if (manifest is null)
            {
                _log.Warning("appmanifest 解析失败：{Path}", manifestPath);
                continue;
            }

            var directory = Path.Combine(library.CommonDirectory, manifest.InstallDir);
            if (Directory.Exists(directory))
            {
                _log.Information("找到游戏目录 {Directory}（buildid {BuildId}）", directory, manifest.BuildId);
                return new GameLocation(directory, manifest);
            }
        }

        return null;
    }

    /// <summary>在各库里找 <c>appmanifest_&lt;appId&gt;.acf</c>，返回游戏目录。</summary>
    public string? FindGameDirectory(string appId, string? steamPath = null)
    {
        steamPath ??= FindSteamPath();
        if (string.IsNullOrWhiteSpace(steamPath))
        {
            _log.Information("没找到 Steam 安装路径");
            return null;
        }

        foreach (var library in ReadLibraries(steamPath))
        {
            var manifestPath = Path.Combine(library.SteamAppsDirectory, $"appmanifest_{appId}.acf");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var manifest = AppManifest.Parse(File.ReadAllText(manifestPath));
            if (manifest is null)
            {
                _log.Warning("appmanifest 解析失败：{Path}", manifestPath);
                continue;
            }

            var directory = Path.Combine(library.CommonDirectory, manifest.InstallDir);
            if (Directory.Exists(directory))
            {
                _log.Information("找到游戏目录 {Directory}（buildid {BuildId}）", directory, manifest.BuildId);
                return directory;
            }
        }

        return null;
    }

    private static string? FindLibraryFoldersFile(string steamPath)
    {
        foreach (var candidate in new[]
                 {
                     Path.Combine(steamPath, "steamapps", "libraryfolders.vdf"),
                     Path.Combine(steamPath, "config", "libraryfolders.vdf"),
                 })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
