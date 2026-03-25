using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static GkmStatus.src.AppConstants;
using static GkmStatus.src.AppSettingsHelper;
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

        public AppConfig Config { get; private set; }

        public ConfigManager()
        {
            Config = new AppConfig();
        }

        public void Load()
        {
            if (!File.Exists(CONFIG_PATH))
            {
                Config = CreateDefaultConfig();
                return;
            }

            try
            {
                string json = File.ReadAllText(CONFIG_PATH);
                Config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? CreateDefaultConfig();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to load config, using default. Error: " + e.Message);
                Config = CreateDefaultConfig();
            }
        }

        public void Save()
        {
            try
            {
                string? dir = Path.GetDirectoryName(CONFIG_PATH);
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(Config, _jsonOptions);
                File.WriteAllText(CONFIG_PATH, json);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to save config. Error: " + e.Message);
            }
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
                    ButtonMode = GetButtonModeString(PresenceButtonMode.Store),
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
        public bool ConnectOnStart { get; set; } = false;
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
        public string ButtonMode { get; set; } = GetButtonModeString(PresenceButtonMode.Store);
        public Dictionary<string, ButtonHistoryData> ButtonHistory { get; set; } = [];
    }

    public class ButtonHistoryData
    {
        public string L1 { get; set; } = ""; public string U1 { get; set; } = "";
        public string L2 { get; set; } = ""; public string U2 { get; set; } = "";
    }

}
