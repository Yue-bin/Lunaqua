using System.Text.Json;
using Serilog;

namespace Lunaqua.Services;

/// <summary>被备份的一个文件。</summary>
/// <param name="OriginalPath">原路径。</param>
/// <param name="BackupPath">备份路径。</param>
public sealed record BackupEntry(string OriginalPath, string BackupPath);

/// <summary>
/// 文件事务：先备份、再落位，任一步失败整体回滚（规格书 §7.2）。
/// </summary>
public sealed class FileTransaction
{
    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _backupRoot;
    private readonly string _basePath;
    private readonly ILogger _log;
    private readonly List<BackupEntry> _backups = [];
    private readonly List<string> _created = [];

    /// <param name="backupRoot">备份目录（例如 &lt;游戏&gt;/.lunaqua/backup/20260909-073000）。</param>
    /// <param name="basePath">用于把原路径换算成备份里的相对路径（通常是游戏根目录）。</param>
    public FileTransaction(string backupRoot, string basePath, ILogger? log = null)
    {
        _backupRoot = backupRoot;
        _basePath = basePath;
        _log = log ?? Log.ForContext<FileTransaction>();
    }

    public IReadOnlyList<BackupEntry> Backups => _backups;

    public IReadOnlyList<string> CreatedFiles => _created;

    /// <summary>目标已存在就先备份，然后复制覆盖。</summary>
    public void Replace(string sourcePath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            BackupFile(destinationPath);
        }
        else
        {
            _created.Add(destinationPath);
        }

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
        _log.Debug("落位 {Destination}", destinationPath);
    }

    /// <summary>备份后删除（用于更新时清理旧版本多出来的文件）。</summary>
    public void Remove(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        BackupFile(path);
        File.Delete(path);
        _log.Debug("删除旧文件 {Path}", path);
    }

    /// <summary>事务成功：写下备份清单（备份本身保留给玩家）。</summary>
    public void Commit()
    {
        if (_backups.Count == 0 && _created.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(_backupRoot);
        var manifest = new
        {
            createdAt = DateTimeOffset.Now,
            basePath = _basePath,
            backups = _backups.Select(entry => new { original = entry.OriginalPath, backup = entry.BackupPath }),
            created = _created,
        };

        File.WriteAllText(
            Path.Combine(_backupRoot, "manifest.json"),
            JsonSerializer.Serialize(manifest, ManifestOptions));

        _log.Information("事务完成：备份 {Backups} 个、新建 {Created} 个（备份在 {Root}）", _backups.Count, _created.Count, _backupRoot);
    }

    /// <summary>失败回滚：删掉本次新建的文件，再把备份复制回去。</summary>
    public void Rollback()
    {
        _log.Warning("回滚事务：{Backups} 个备份、{Created} 个新建文件", _backups.Count, _created.Count);

        foreach (var created in _created)
        {
            TryDelete(created);
        }

        foreach (var (original, backup) in _backups)
        {
            try
            {
                var directory = Path.GetDirectoryName(original);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(backup, original, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Error(ex, "回滚失败，请手动从 {Backup} 恢复 {Original}", backup, original);
            }
        }

        TryDeleteDirectoryIfEmpty(_backupRoot);
    }

    private void BackupFile(string path)
    {
        var relative = Path.GetRelativePath(_basePath, path);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            relative = $"{_backups.Count}-{Path.GetFileName(path)}";
        }

        var backupPath = Path.Combine(_backupRoot, relative);
        var directory = Path.GetDirectoryName(backupPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(path, backupPath, overwrite: true);
        _backups.Add(new BackupEntry(path, backupPath));
    }

    private void TryDelete(string path)
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
            _log.Warning(ex, "回滚时删不掉 {Path}", path);
        }
    }

    private void TryDeleteDirectoryIfEmpty(string path)
    {
        try
        {
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Debug(ex, "清不掉空备份目录 {Path}", path);
        }
    }
}
