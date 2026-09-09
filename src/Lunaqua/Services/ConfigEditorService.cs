using Lunaqua.Domain;
using Lunaqua.Infrastructure.BepInEx;
using Serilog;

namespace Lunaqua.Services;

/// <summary>配置编辑器：列 .cfg、按 GUID 关联 mod、备份后写回（规格书 §9）。</summary>
public sealed class ConfigEditorService
{
    private readonly IGameProcessDetector _processDetector;
    private readonly ILogger _log;
    private readonly HashSet<string> _backedUpThisSession = new(StringComparer.OrdinalIgnoreCase);

    public ConfigEditorService(IGameProcessDetector processDetector, ILogger? log = null)
    {
        _processDetector = processDetector;
        _log = log ?? Log.ForContext<ConfigEditorService>();
    }

    /// <summary>游戏运行中 → 只读。</summary>
    public bool IsGameRunning => _processDetector.IsGameRunning();

    public static string ConfigDirectory(GameContext game) => Path.Combine(game.GameDirectory, "BepInEx", "config");

    /// <summary>列出全部 .cfg（含未关联任何已装 mod 的）。</summary>
    public IReadOnlyList<string> ListConfigPaths(GameContext game)
    {
        var directory = ConfigDirectory(game);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(directory, "*.cfg").OrderBy(path => path, StringComparer.OrdinalIgnoreCase)];
    }

    public CfgFile? Load(string path)
    {
        try
        {
            return CfgFile.Load(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "读取配置文件失败：{Path}", path);
            return null;
        }
    }

    /// <summary>按 <c>## Plugin GUID:</c> 找某个 mod 的配置；没找到返回 null。</summary>
    public CfgFile? FindForGuid(GameContext game, string? guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
        {
            return null;
        }

        foreach (var path in ListConfigPaths(game))
        {
            var file = Load(path);
            if (file is not null && string.Equals(file.PluginGuid, guid, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }

    /// <summary>写回（首次编辑前先备份）。</summary>
    public void Save(GameContext game, CfgFile file)
    {
        if (IsGameRunning)
        {
            throw new InvalidOperationException("游戏正在运行，配置只能看不能改。");
        }

        BackupOnce(game, file);
        file.Save();
        _log.Information("配置已写回：{Path}", file.Path);
    }

    /// <summary>本次会话里第一次改这个文件时备份一份。</summary>
    public string? BackupOnce(GameContext game, CfgFile file)
    {
        if (!_backedUpThisSession.Add(file.Path))
        {
            return null;
        }

        return Backup(game, file);
    }

    /// <summary>备份到 <c>&lt;游戏&gt;/.lunaqua/backup/config/&lt;guid 或文件名&gt;/&lt;时间戳&gt;.cfg</c>。</summary>
    public string? Backup(GameContext game, CfgFile file)
    {
        if (!File.Exists(file.Path))
        {
            return null;
        }

        var name = string.IsNullOrWhiteSpace(file.PluginGuid)
            ? Path.GetFileNameWithoutExtension(file.Path)
            : file.PluginGuid!;

        var directory = Path.Combine(game.LunaquaDirectory, "backup", "config", name);
        Directory.CreateDirectory(directory);

        var target = Path.Combine(directory, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".cfg");
        File.Copy(file.Path, target, overwrite: true);
        _log.Information("配置已备份：{Target}", target);
        return target;
    }
}
