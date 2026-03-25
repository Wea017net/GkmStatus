using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using static GkmStatus.src.AppConstants;

namespace GkmStatus.src.ui
{
    public static class I18n
    {
        public enum Language
        {
            Japanese,
            English
        }

        private static Language currentLanguage = Language.Japanese;
        private static Dictionary<string, string> currentTranslations = [];
        public static Language CurrentLanguage
        {
            get => currentLanguage;
            set => SetLanguage(value);
        }

        public enum Text_List
        {
            App_Name,
            Menu_File,
            Menu_Exit,
            Menu_Settings,
            Menu_RunAtStartup,
            Menu_StartMinimized,
            Menu_AutoConnect,
            Menu_CheckForUpdates,
            Menu_NotifyInBackground,
            Menu_NotifyOnMinimize,
            Menu_MinimizeToTray,
            Menu_MonitorProcess,
            Menu_Language,
            Menu_View,
            Menu_Theme,
            Menu_ThemeAuto,
            Menu_ThemeLight,
            Menu_ThemeDark,
            Menu_ThemeOLED,
            Menu_Help,
            Menu_Github,
            Menu_CheckUpdateNow,
            Menu_About,
            Menu_OpenAppLocation,
            Menu_OpenConfigLocation,
            Tray_Open,
            Tray_ProducingIdol,
            Tray_Exit,

            Header_GameName,
            Header_Details,
            Header_State,
            Header_Timestamp,
            Header_Buttons,

            Details_None,
            Details_PName,
            Details_PLv,
            Details_Both,
            Placeholder_PName,

            State_None,
            State_PID,
            State_Producing,
            State_Idol,
            State_Custom,
            Placeholder_PID,
            Placeholder_Custom,
            State_ProducingSuffix,
            State_IdolSuffix,
            State_NotSet,
            State_Producing_Format,
            State_Idol_Format,

            Timestamp_Label,
            Timestamp_Reset,
            Timestamp_Guide,

            Button_Mode,
            Button_ModeNone,
            Button_ModeStore,
            Button_ModeApp,
            Button_ModeCustom,
            Button_ModeNote,
            Placeholder_BtnLabel,
            Placeholder_BtnUrl,
            Button_Warning_LabelLength,
            Button_Warning_UrlJp,

            Status_Connected,
            Status_Connected_Notify,
            Status_Disconnected,
            Status_Disconnected_Auto,
            Status_Disconnected_Notify,
            Status_ManualDisconnected_Notify,
            Status_Connecting,
            Status_Paused,
            Status_Timeout,
            Status_Error,
            Status_InitFailed,
            Status_Exception,
            Status_Updated,
            Tray_Status_Connected,
            Tray_Status_Paused,
            Tray_Status_Connecting,

            Button_Connect,
            Button_Pause,
            Button_Resume,
            Button_Update,
            Button_Disconnect,
            Button_StoreLabel_Mobile,
            Button_StoreLabel_DMM,
            Button_AboutPresence,

            GameApp_Guide,
            About_Message,
            About_Author,
            About_FontAttribution,
            About_FontLicense,
            About_Title,
            Error_Browser,
            CharName_JP,
            CharName_EN,
            Update_NotificationTitle,
            Update_NotificationBody,
            Update_NewAvailable,
            Update_NoUpdate,
            Update_RateLimit,
            Notify_Minimized,
            Notify_TrayIdolChanged,
            Notify_TrayDetailsChanged,
            Notify_TrayStateChanged,
        }

        static I18n()
        {
            SetLanguage(currentLanguage);
        }

        public static void SetLanguage(Language lang)
        {
            currentLanguage = lang;
            currentTranslations = LoadTranslations(currentLanguage);
        }

        public static string T(string key)
        {
            if (currentTranslations.TryGetValue(key, out var value))
            {
                return value;
            }
            return key; // Fallback to key name
        }

        public static string T(Text_List key) => T(key.ToString());

        public static string T(string key, params object[] args) => string.Format(T(key), args);

        public static string T(Text_List key, params object[] args) => string.Format(T(key), args);

        private static Dictionary<string, string> LoadTranslations(Language lang)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var (resourceName, rootKey) = GetResourceInfo(lang);

            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    Debug.WriteLine($"I18n resource not found: {resourceName}");
                    return map;
                }

                using var doc = JsonDocument.Parse(stream);
                JsonElement entries = default;
                bool found = false;

                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty(rootKey, out var langArray) && langArray.ValueKind == JsonValueKind.Array)
                    {
                        entries = langArray;
                        found = true;
                    }
                    else
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                entries = prop.Value;
                                found = true;
                                break;
                            }
                        }
                    }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    entries = doc.RootElement;
                    found = true;
                }

                if (!found)
                    return map;

                foreach (var item in entries.EnumerateArray())
                {
                    if (!item.TryGetProperty("key", out var keyEl) || !item.TryGetProperty("text", out var textEl))
                        continue;

                    var key = keyEl.GetString();
                    var text = textEl.GetString() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(key))
                        map[key] = text;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("I18n load error: " + ex.Message);
            }

            return map;
        }

        private static (string ResourceName, string RootKey) GetResourceInfo(Language language)
        {
            if (language == Language.Japanese)
                return (Localization_Data_Path + ".japanese.json", "japanese");

            return (Localization_Data_Path + ".english.json", "english");
        }
    }
}
