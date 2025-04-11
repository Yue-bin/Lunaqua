using I18N.Avalonia.Interface;
using Lunaqua.Models;
using Splat;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Lunaqua.Services
{
    public class ConfigService
    {
        [JsonIgnore]
        public static readonly string ConfigDir = Path.Combine(Environment.CurrentDirectory, "Config.json");

        [JsonIgnore] public static ConfigService? Instance;

        private ConfigService()
        {
            if (!File.Exists(ConfigDir))
            {
                AppLanguage = Thread.CurrentThread.CurrentCulture.Name;
            }
            else
                try
                {
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(ConfigDir));
                    var themeMode = jsonElement.GetProperty("theme").GetInt32() switch
                    {
                        0 => ThemeModeEnum.Light,
                        1 => ThemeModeEnum.Dark,
                        _ => ThemeModeEnum.SyncWithSystem
                    };
                    AppTheme = themeMode;
                    var property = jsonElement.GetProperty("language");
                    AppLanguage = property.GetString() ?? Thread.CurrentThread.CurrentCulture.Name;
                }
                catch
                {
                    AppLanguage = Thread.CurrentThread.CurrentCulture.Name;
                }
        }

        [JsonPropertyName("theme")] public ThemeModeEnum AppTheme { get; set; } = ThemeModeEnum.SyncWithSystem;

        [JsonPropertyName("language")]
        public static string AppLanguage
        {
            get => Locator.Current.GetService<ILocalizer>()!.Language.Name;
            set => Locator.Current.GetService<ILocalizer>()!.Language = new CultureInfo(value);
        }

        public CultureInfo GetAppCultureInfo() => new(AppLanguage);

        public static void LoadConfig() => Instance = new ConfigService();

        public bool Save()
        {
            try
            {
                var contents = JsonSerializer.Serialize(this);
                File.WriteAllText(ConfigDir, contents);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }

            return true;
        }
    }
}