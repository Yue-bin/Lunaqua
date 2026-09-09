using System.Text.Json;
using System.Text.Json.Serialization;
using Lunaqua.Domain;
using Serilog;

namespace Lunaqua.Services;

/// <summary>凭证的读写（规格书 §7.1：落位清单、来源、启停状态、忽略版本）。</summary>
public sealed class CredentialStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ILogger _log;

    public CredentialStore(ILogger? log = null) => _log = log ?? Log.ForContext<CredentialStore>();

    public InstalledMod? Read(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<InstalledMod>(File.ReadAllText(path), Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "凭证读取失败：{Path}", path);
            return null;
        }
    }

    public void Write(string path, InstalledMod credential)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(credential, Options));
        File.Move(tempPath, path, overwrite: true);
        _log.Information("已写凭证：{Path}", path);
    }

    public void Delete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            _log.Information("已删凭证：{Path}", path);
        }
    }

    /// <summary>扫描游戏目录里全部凭证（含 UVFS 的 mods 与 mods.disabled）。</summary>
    public IReadOnlyList<InstalledMod> Enumerate(GameContext game)
    {
        var result = new List<InstalledMod>();

        foreach (var root in new[] { game.LunaquaDirectory, game.OverlayModsDirectory, game.OverlayDisabledDirectory })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(root))
            {
                var credential = Read(Path.Combine(directory, "info.json"));
                if (credential is not null)
                {
                    result.Add(credential);
                }
            }
        }

        return result;
    }
}
