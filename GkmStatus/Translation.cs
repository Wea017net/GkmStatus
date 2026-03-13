using System.Collections.Generic;

namespace GkmStatus
{
    public static class I18n
    {
        public static string CurrentLanguage { get; set; } = "日本語";

        private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
        {
            ["日本語"] = new Dictionary<string, string>
            {
                ["App_Name"] = "学マステータス",

                ["Menu_File"] = "ファイル(&F)",
                ["Menu_Exit"] = "終了",
                ["Menu_Settings"] = "設定(&S)",
                ["Menu_RunAtStartup"] = "システム起動時に実行",
                ["Menu_StartMinimized"] = "最小化した状態(システムトレイ内)で起動",
                ["Menu_AutoConnect"] = "起動時に接続を試行",
                ["Menu_CheckForUpdates"] = "起動時にアプリの更新を確認",
                ["Menu_NotifyInBackground"] = "バックグラウンド動作時に通知",
                ["Menu_NotifyOnMinimize"] = "最小化時に通知",
                ["Menu_MinimizeToTray"] = "×でアプリを最小化(システムトレイに格納)",
                ["Menu_MonitorProcess"] = "gakumas.exeを監視して自動で接続/切断",
                ["Menu_Language"] = "言語",
                ["Menu_View"] = "表示(&V)",
                ["Menu_Theme"] = "テーマ",
                ["Menu_ThemeAuto"] = "自動選択",
                ["Menu_ThemeLight"] = "ライト",
                ["Menu_ThemeDark"] = "ダーク",
                ["Menu_ThemeOLED"] = "OLED",
                ["Menu_Help"] = "ヘルプ(&H)",
                ["Menu_Github"] = "GitHubページを開く...",
                ["Menu_CheckUpdateNow"] = "更新を確認...",
                ["Menu_About"] = "このアプリについて...",
                ["Menu_OpenAppLocation"] = "このアプリの場所を開く...",
                ["Menu_OpenConfigLocation"] = "設定ファイルの場所を開く...",
                ["Tray_Open"] = "開く",
                ["Tray_ProducingIdol"] = "プロデュース中のアイドル",
                ["Tray_Exit"] = "終了",

                ["Header_GameName"] = "ゲームの表示名 (Name)",
                ["Header_Details"] = "1行目の情報 (Details)",
                ["Header_State"] = "2行目の情報 (State)",
                ["Header_Timestamp"] = "経過時間 (Timestamp)",
                ["Header_Buttons"] = "外部リンクボタン設定 (Buttons)",

                ["Details_None"] = "なし",
                ["Details_PName"] = "プロデューサー名",
                ["Details_PLv"] = "プロデューサーレベル",
                ["Details_Both"] = "プロデューサー名とPLv",
                ["Placeholder_PName"] = "プロデューサー",

                ["State_None"] = "なし",
                ["State_PID"] = "P-ID",
                ["State_Producing"] = "プロデュース中",
                ["State_Idol"] = "担当アイドル",
                ["State_Custom"] = "カスタム",
                ["Placeholder_PID"] = "8桁のP-IDを入力",
                ["Placeholder_Custom"] = "自由入力 (全角: 2～42 文字 / 半角: 2～128 文字)",
                ["State_ProducingSuffix"] = "をプロデュース中",
                ["State_IdolSuffix"] = " 担当",
                ["State_NotSet"] = "未設定",
                ["State_Producing_Format"] = "{0}をプロデュース中",
                ["State_Idol_Format"] = "{0} 担当",

                ["Timestamp_Label"] = "経過時間",
                ["Timestamp_Reset"] = "時間をリセット",
                ["Timestamp_Guide"] = "「更新」を押すと反映されます",

                ["Button_Mode"] = "ボタン表示モード",
                ["Button_ModeNone"] = "なし",
                ["Button_ModeStore"] = "ゲームに誘導",
                ["Button_ModeApp"] = "このアプリに誘導",
                ["Button_ModeCustom"] = "カスタム",
                ["Placeholder_BtnLabel"] = "ボタン{0}つ目のラベル",
                ["Placeholder_BtnUrl"] = "ボタン{0}つ目のURL",
                ["Button_Warning_LabelLength"] = "全角の場合は最大10文字まで表示されます。",
                ["Button_Warning_UrlJp"] = "URLに日本語を含めることはできません。\nパーセントエンコーディング後の文字列を使用してください。",

                ["Status_Connected"] = "接続中:\n{0}",
                ["Status_Connected_Notify"] = "Discordに接続してステータスの表示を開始しました。",
                ["Status_Disconnected"] = "未接続",
                ["Status_Disconnected_Notify"] = "Discordへのステータス表示を停止しました。",
                ["Status_ManualDisconnected_Notify"] = "アプリとDiscordの接続を切断しました。",
                ["Status_Connecting"] = "接続試行中... ({0}秒)",
                ["Status_Paused"] = "接続済み\n（表示を停止中）",
                ["Status_Timeout"] = "タイムアウト:\nDiscordの起動を要確認",
                ["Status_Error"] = "エラー: {0}",
                ["Status_InitFailed"] = "初期化失敗。",
                ["Status_Exception"] = "例外: {0}",
                ["Status_Updated"] = "更新しました",
                ["Tray_Status_Connected"] = "接続中",
                ["Tray_Status_Paused"] = "表示停止中",
                ["Tray_Status_Connecting"] = "接続試行中",

                ["Button_Connect"] = "接続",
                ["Button_Pause"] = "一時停止",
                ["Button_Update"] = "更新",
                ["Button_Disconnect"] = "切断",
                ["Button_StoreLabel_Mobile"] = "プレイ (iOS / Android)",
                ["Button_StoreLabel_DMM"] = "プレイ (DMM GAMES)",
                ["Button_AboutPresence"] = "この表示について...",

                ["GameApp_Guide"] = "この変更は一度切断して\n再接続したあとに適用されます",
                ["About_Message"] = "GkmStatus\nv{0}\n\n制作者: Wea017net",
                ["About_Author"] = "制作者: Wea017net",
                ["About_FontAttribution"] = "使用フォント: IBM Plex Sans JP",
                ["About_FontLicense"] = "ライセンスを表示",
                ["About_Title"] = "このアプリについて",
                ["Error_Browser"] = "ブラウザを開けませんでした。",
                ["CharName_JP"] = "日本語表記",
                ["CharName_EN"] = "英語表記",
                ["Update_NotificationTitle"] = "新しいバージョンが利用可能です",
                ["Update_NotificationBody"] = "新バージョン {0} が公開されています。\nクリックしてダウンロードページを開きます。",
                ["Update_NewAvailable"] = "新しいバージョン {0} が公開されています。\nダウンロードページを開きますか？",
                ["Update_NoUpdate"] = "最新バージョンを使用しています。",
                ["Update_RateLimit"] = "GitHub APIの回数制限に達しました。しばらく時間をおいてから再度お試しください。",
                ["Notify_Minimized"] = "システムトレイに最小化しました。",
                ["Notify_TrayIdolChanged"] = "プロデュース中のアイドルの変更を適用して情報を更新しました。",
                ["Notify_TrayDetailsChanged"] = "1行目の情報(Details)の変更を適用して情報を更新しました。",
                ["Notify_TrayStateChanged"] = "2行目の情報(State)の変更を適用して情報を更新しました。"
            },
            ["English"] = new Dictionary<string, string>
            {
                ["App_Name"] = "GkmStatus",

                ["Menu_File"] = "&File",
                ["Menu_Exit"] = "Exit",
                ["Menu_Settings"] = "&Settings",
                ["Menu_RunAtStartup"] = "Run at system startup",
                ["Menu_StartMinimized"] = "Start minimized in tray",
                ["Menu_AutoConnect"] = "Try connecting on startup",
                ["Menu_CheckForUpdates"] = "Check for updates on startup",
                ["Menu_NotifyInBackground"] = "Notify in background",
                ["Menu_NotifyOnMinimize"] = "Notify on minimize",
                ["Menu_MinimizeToTray"] = "Minimize to tray on close",
                ["Menu_MonitorProcess"] = "Monitor gakumas.exe and auto connect/disconnect",
                ["Menu_Language"] = "Language",
                ["Menu_View"] = "&View",
                ["Menu_Theme"] = "Theme",
                ["Menu_ThemeAuto"] = "Automatic",
                ["Menu_ThemeLight"] = "Light",
                ["Menu_ThemeDark"] = "Dark",
                ["Menu_ThemeOLED"] = "OLED",
                ["Menu_Help"] = "&Help",
                ["Menu_Github"] = "Open GitHub page...",
                ["Menu_CheckUpdateNow"] = "Check for Updates...",
                ["Menu_About"] = "About this app...",
                ["Menu_OpenAppLocation"] = "Open app location...",
                ["Menu_OpenConfigLocation"] = "Open config location...",
                ["Tray_Open"] = "Open",
                ["Tray_ProducingIdol"] = "Producing Idol",
                ["Tray_Exit"] = "Exit",

                ["Header_GameName"] = "Game Display Name (Name)",
                ["Header_Details"] = "Line 1 Info (Details)",
                ["Header_State"] = "Line 2 Info (State)",
                ["Header_Timestamp"] = "Elapsed Time (Timestamp)",
                ["Header_Buttons"] = "External Link Settings (Buttons)",

                ["Details_None"] = "None",
                ["Details_PName"] = "Producer Name",
                ["Details_PLv"] = "Producer Level",
                ["Details_Both"] = "P-Name and PLv",
                ["Placeholder_PName"] = "Producer",

                ["State_None"] = "None",
                ["State_PID"] = "P-ID",
                ["State_Producing"] = "Producing...",
                ["State_Idol"] = "In Charge",
                ["State_Custom"] = "Custom",
                ["Placeholder_PID"] = "Enter 8-digit P-ID",
                ["Placeholder_Custom"] = "Enter text (2-128 characters)",
                ["State_ProducingSuffix"] = " producing",
                ["State_IdolSuffix"] = "Producer of ",
                ["State_NotSet"] = "Not Set",
                ["State_Producing_Format"] = "Producing {0}",
                ["State_Idol_Format"] = "Producer of {0}",

                ["Timestamp_Label"] = "Elapsed",
                ["Timestamp_Reset"] = "Reset Timer",
                ["Timestamp_Guide"] = "Click 'Update' to apply",

                ["Button_Mode"] = "Button Mode",
                ["Button_ModeNone"] = "None",
                ["Button_ModeStore"] = "Link to Game",
                ["Button_ModeApp"] = "Link to this app",
                ["Button_ModeCustom"] = "Custom",
                ["Placeholder_BtnLabel"] = "Button {0} Label",
                ["Placeholder_BtnUrl"] = "Button {0} URL",
                ["Button_Warning_LabelLength"] = "Multi-byte chars will show up to 10 chars.",
                ["Button_Warning_UrlJp"] = "Japanese characters are not allowed in URLs.\nPlease use percent-encoded strings.",

                ["Status_Disconnected"] = "Disconnected",
                ["Status_Connected"] = "Connected:\n{0}",
                ["Status_Connected_Notify"] = "Connected to Discord and started displaying status.",
                ["Status_Disconnected_Notify"] = "Disconnected from Discord.",
                ["Status_ManualDisconnected_Notify"] = "Disconnected from Discord manually.",
                ["Status_Connecting"] = "Connecting... ({0}s)",
                ["Status_Paused"] = "Connected\n(Display Paused)",
                ["Status_Timeout"] = "Timeout: \nPlease check Discord.",
                ["Status_Error"] = "Error: {0}",
                ["Status_InitFailed"] = "Initialization Failed.",
                ["Status_Exception"] = "Exception: {0}",
                ["Status_Updated"] = "Updated",
                ["Tray_Status_Connected"] = "Connected",
                ["Tray_Status_Paused"] = "Paused",
                ["Tray_Status_Connecting"] = "Connecting",

                ["Button_Connect"] = "Connect",
                ["Button_Pause"] = "Pause",
                ["Button_Update"] = "Update",
                ["Button_Disconnect"] = "Disconnect",
                ["Button_StoreLabel_Mobile"] = "Play (iOS / Android)",
                ["Button_StoreLabel_DMM"] = "Play (DMM GAMES)",
                ["Button_AboutPresence"] = "About this presence...",

                ["GameApp_Guide"] = "Changes apply after\ndisconnecting and reconnecting",
                ["About_Message"] = "GkmStatus\nv{0}\n\nMade by: Wea017net",
                ["About_Author"] = "Made by: Wea017net",
                ["About_FontAttribution"] = "Font: IBM Plex Sans JP",
                ["About_FontLicense"] = "View License",
                ["About_Title"] = "About this app",
                ["Error_Browser"] = "Could not open the browser.",
                ["CharName_JP"] = "Japanese Names",
                ["CharName_EN"] = "English Names",
                ["Update_NotificationTitle"] = "New Version Available",
                ["Update_NotificationBody"] = "Version {0} is now available.\nClick here to open the download page.",
                ["Update_NewAvailable"] = "A new version {0} is available.\nWould you like to open the download page?",
                ["Update_NoUpdate"] = "You are using the latest version.",
                ["Update_RateLimit"] = "GitHub API rate limit exceeded. Please try again later.",
                ["Notify_Minimized"] = "Minimized to system tray.",
                ["Notify_TrayIdolChanged"] = "Applied the producing idol change and updated the presence.",
                ["Notify_TrayDetailsChanged"] = "Applied the Details change and updated the presence.",
                ["Notify_TrayStateChanged"] = "Applied the State change and updated the presence."
            }
        };

        public static string T(string key)
        {
            if (Translations.TryGetValue(CurrentLanguage, out var locale) && locale.TryGetValue(key, out var value))
            {
                return value;
            }
            return key; // Fallback to key name
        }

        public static string T(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }
    }
}
