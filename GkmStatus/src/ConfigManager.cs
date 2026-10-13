using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static GkmStatus.src.AppConstants;
using static GkmStatus.src.AppSettingsHelper;
using static GkmStatus.src.Characters;
using GkmStatus.src.ui;

namespace GkmStatus.src
{
    public class ConfigManager
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
        };

        private readonly string _configPath;

        public AppConfig Config { get; private set; }
        public string? LastSaveError { get; private set; }

        public ConfigManager(string? configPath = null)
        {
            _configPath = configPath ?? CONFIG_PATH;
            Config = new AppConfig();
        }

        public void Load()
        {
            if (!File.Exists(_configPath))
            {
                Config = CreateDefaultConfig();
                return;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(_configPath));
                Config = ReadConfig(document.RootElement);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to load config, using default. Error: " + e.Message);
                Config = CreateDefaultConfig();
            }
        }

        public bool Save()
        {
            string? tempPath = null;
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(_configPath))!;
                Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(Config, _jsonOptions);
                tempPath = Path.Combine(dir, $".{Path.GetFileName(_configPath)}.{Guid.NewGuid():N}.tmp");
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                File.Move(tempPath, _configPath, overwrite: true);
                tempPath = null;
                LastSaveError = null;
                return true;
            }
            catch (Exception e)
            {
                LastSaveError = e.Message;
                Debug.WriteLine("Failed to save config. Error: " + e);
                return false;
            }
            finally
            {
                if (tempPath != null)
                {
                    try { File.Delete(tempPath); }
                    catch (Exception e) { Debug.WriteLine("Failed to remove temporary config file: " + e.Message); }
                }
            }
        }

        private static AppConfig ReadConfig(JsonElement root)
        {
            var config = CreateDefaultConfig();
            if (root.ValueKind != JsonValueKind.Object)
                return config;

            config.ConfigVersion = Read(root, "ConfigVersion", config.ConfigVersion);
            if (TryGetProperty(root, "Settings", out var settings) && settings.ValueKind == JsonValueKind.Object)
            {
                config.Settings.StartMinimized = Read(settings, "StartMinimized", config.Settings.StartMinimized);
                config.Settings.ConnectOnStart = Read(settings, "ConnectOnStart", config.Settings.ConnectOnStart);
                config.Settings.AutoCheckUpdates = Read(settings, "AutoCheckUpdates", config.Settings.AutoCheckUpdates);
                config.Settings.ShowBackgroundNotifications = Read(settings, "ShowBackgroundNotifications", config.Settings.ShowBackgroundNotifications);
                config.Settings.NotifyOnMinimize = Read(settings, "NotifyOnMinimize", config.Settings.NotifyOnMinimize);
                config.Settings.MinimizeToTray = Read(settings, "MinimizeToTray", config.Settings.MinimizeToTray);
                config.Settings.AutoDetectGakumas = Read(settings, "AutoDetectGakumas", config.Settings.AutoDetectGakumas);
                config.Settings.SelectedTheme = GetThemeString(GetTheme(Read(settings, "SelectedTheme", config.Settings.SelectedTheme)));
                config.Settings.SelectedLanguage = GetLangString(GetLanguage(Read(settings, "SelectedLanguage", config.Settings.SelectedLanguage)));
                config.Settings.LastUpdateCheck = Read(settings, "LastUpdateCheck", config.Settings.LastUpdateCheck);
                if (config.Settings.LastUpdateCheck > DateTime.UtcNow)
                    config.Settings.LastUpdateCheck = DateTime.MinValue;
            }

            if (TryGetProperty(root, "Presence", out var presence) && presence.ValueKind == JsonValueKind.Object)
            {
                var value = config.Presence;
                string? defaultIdolId = value.SelectedIdolCharacterId;
                string? defaultProduceId = value.SelectedProduceCharacterId;
                value.DetailsType = GetDetailsString(GetDetailsType(Read(presence, "DetailsType", value.DetailsType)));
                value.ProducerName = Read(presence, "ProducerName", value.ProducerName);
                value.ProducerLevel = Math.Clamp(Read(presence, "ProducerLevel", value.ProducerLevel), 1, 100);
                value.StateType = GetStateString(GetStateType(Read(presence, "StateType", value.StateType)));
                value.CharNameLangIndex = Math.Clamp(Read(presence, "CharNameLangIndex", value.CharNameLangIndex), 0, 1);
                value.SelectedIdolCharacterId = Read(presence, "SelectedIdolCharacterId", value.SelectedIdolCharacterId);
                value.SelectedProduceCharacterId = Read(presence, "SelectedProduceCharacterId", value.SelectedProduceCharacterId);
                var characterIds = ProduceCharacters.Select(character => character.Id).ToHashSet(StringComparer.Ordinal);
                if (value.SelectedIdolCharacterId == null || !characterIds.Contains(value.SelectedIdolCharacterId))
                    value.SelectedIdolCharacterId = defaultIdolId;
                if (value.SelectedProduceCharacterId == null || !characterIds.Contains(value.SelectedProduceCharacterId))
                    value.SelectedProduceCharacterId = defaultProduceId;
                value.GameAppIndex = Math.Clamp(Read(presence, "GameAppIndex", value.GameAppIndex), 0, GameApps.Count - 1);
                value.ButtonMode = GetButtonModeString(GetButtonMode(Read(presence, "ButtonMode", value.ButtonMode)));
                value.StateHistory = ReadStateHistory(presence);
                value.ButtonHistory = ReadButtonHistory(presence);
            }
            return config;
        }

        private static T Read<T>(JsonElement element, string name, T fallback)
        {
            if (!TryGetProperty(element, name, out var property))
                return fallback;
            try
            {
                var value = JsonSerializer.Deserialize<T>(property.GetRawText(), _jsonOptions);
                return value is null ? fallback : value;
            }
            catch
            {
                return fallback;
            }
        }

        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }
            value = default;
            return false;
        }

        private static Dictionary<string, string> ReadStateHistory(JsonElement presence)
        {
            var result = new Dictionary<string, string>();
            if (!TryGetProperty(presence, "StateHistory", out var history) || history.ValueKind != JsonValueKind.Object)
                return result;
            foreach (var property in history.EnumerateObject())
            {
                if (Enum.TryParse<PresenceStateType>(property.Name, out var state) && Enum.IsDefined(state)
                    && property.Value.ValueKind == JsonValueKind.String)
                    result[state.ToString()] = property.Value.GetString() ?? string.Empty;
            }
            return result;
        }

        private static Dictionary<string, ButtonHistoryData> ReadButtonHistory(JsonElement presence)
        {
            var result = new Dictionary<string, ButtonHistoryData>();
            if (!TryGetProperty(presence, "ButtonHistory", out var history) || history.ValueKind != JsonValueKind.Object)
                return result;
            foreach (var property in history.EnumerateObject())
            {
                if (!Enum.TryParse<PresenceButtonMode>(property.Name, out var mode) || !Enum.IsDefined(mode)
                    || property.Value.ValueKind != JsonValueKind.Object)
                    continue;
                result[mode.ToString()] = new ButtonHistoryData
                {
                    L1 = Read(property.Value, "L1", string.Empty),
                    U1 = Read(property.Value, "U1", string.Empty),
                    L2 = Read(property.Value, "L2", string.Empty),
                    U2 = Read(property.Value, "U2", string.Empty)
                };
            }
            return result;
        }

        public void ResetToDefault()
        {
            Config = CreateDefaultConfig();
        }

        public static AppConfig CreateDefaultConfig()
        {
            var isJapanese = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ja");

            return new AppConfig
            {
                Settings = new AppSettings
                {
                    AutoDetectGakumas = true,
                    AutoCheckUpdates = true,
                    ShowBackgroundNotifications = true,
                    NotifyOnMinimize = true,
                    MinimizeToTray = true,
                    SelectedTheme = GetThemeString(0),
                    SelectedLanguage = GetLangString(isJapanese ? I18n.Language.Japanese : I18n.Language.English)
                },
                Presence = new PresenceSettings
                {
                    StateType = GetStateString(PresenceStateType.Idol),
                    ButtonMode = GetButtonModeString(PresenceButtonMode.App),
                    DetailsType = GetDetailsString(PresenceDetailsType.Both)
                }
            };
        }
    }

    public class AppConfig
    {
        public int ConfigVersion { get; set; } = 1;
        public AppSettings Settings { get; set; } = new AppSettings();
        public PresenceSettings Presence { get; set; } = new PresenceSettings();
    }

    public class AppSettings
    {
        public bool StartMinimized { get; set; } = false;
        public bool ConnectOnStart { get; set; } = true;
        public bool AutoCheckUpdates { get; set; } = true;
        public bool ShowBackgroundNotifications { get; set; } = true;
        public bool NotifyOnMinimize { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool AutoDetectGakumas { get; set; } = true;
        public string SelectedTheme { get; set; } = GetThemeString(0);
        public string SelectedLanguage { get; set; } = GetLangString(I18n.Language.Japanese);
        public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;
    }

    public class PresenceSettings
    {
        public string DetailsType { get; set; } = GetDetailsString(PresenceDetailsType.Both);
        public string ProducerName { get; set; } = "";
        public int ProducerLevel { get; set; } = 1;
        public string StateType { get; set; } = GetStateString(PresenceStateType.Producing);
        public int CharNameLangIndex { get; set; } = 0;
        public string? SelectedIdolCharacterId { get; set; } = "hanami_saki";
        public string? SelectedProduceCharacterId { get; set; } = "hanami_saki";
        public Dictionary<string, string> StateHistory { get; set; } = [];
        public int GameAppIndex { get; set; } = 0;
        public string ButtonMode { get; set; } = GetButtonModeString(PresenceButtonMode.App);
        public Dictionary<string, ButtonHistoryData> ButtonHistory { get; set; } = [];
    }

    public class ButtonHistoryData
    {
        public string L1 { get; set; } = ""; public string U1 { get; set; } = "";
        public string L2 { get; set; } = ""; public string U2 { get; set; } = "";
    }

}
