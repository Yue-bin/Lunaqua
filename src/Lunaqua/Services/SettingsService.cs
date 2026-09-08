using System.Text.Json;
using System.Text.Json.Serialization;
using Lunaqua.Models;
using Serilog;

namespace Lunaqua.Services;

/// <summary>settings.json 的读写实现。</summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _filePath;
    private readonly ILogger _log;

    /// <param name="filePath">设置文件路径；默认 <see cref="AppPaths.SettingsFile"/>。</param>
    /// <param name="log">日志；默认 Serilog 的上下文日志。</param>
    public SettingsService(string? filePath = null, ILogger? log = null)
    {
        _filePath = filePath ?? AppPaths.SettingsFile;
        _log = log ?? Log.ForContext<SettingsService>();
    }

    public AppSettings Current { get; private set; } = new();

    public event EventHandler? Changed;

    public void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _log.Information("设置文件不存在，使用默认设置：{Path}", _filePath);
                Current = new AppSettings();
                return;
            }

            var json = File.ReadAllText(_filePath);
            Current = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
            _log.Information("已载入设置：{Path}", _filePath);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "设置文件不可用，回退默认设置：{Path}", _filePath);
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(Current, SerializerOptions));
            File.Move(tempPath, _filePath, overwrite: true);
            _log.Debug("设置已保存：{Path}", _filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error(ex, "设置保存失败：{Path}", _filePath);
        }
    }

    public void Update(Action<AppSettings> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        mutate(Current);
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
