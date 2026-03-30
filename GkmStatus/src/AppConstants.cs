using System;
using System.Collections.Generic;
using System.Text;

namespace GkmStatus.src
{
    internal class AppConstants
    {
        public const string PROCESS_NAME = "gakumas";
        public const string APP_NAME = "GkmStatus";
        private const string RESOURCE_PREFIX = "GkmStatus.Resources.";


        public static readonly List<(string Name, string AppId)> GameApps =
        [
            ("学園アイドルマスター", "1352261574877778001"),
            ("学マス", "1467733389170835486"),
            ("THE IDOLM@STER Gakuen", "1467733691382890499"),
            ("Gakuen iDOLM@STER", "1467734377197867040"),
            ("Gakumas", "1467734892208193650")
        ];

        public const string GITHUB_REPO_URL = "https://api.github.com/repos/Wea017net/GkmStatus/releases/latest";
        public const string HTTP_USER_AGENT = "GkmStatus-UpdateChecker";

        public const string REG_RUN_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public const string ProduceCharacter_Data_Path = RESOURCE_PREFIX + "produce_characters.json";
        public const string Localization_Data_Path = RESOURCE_PREFIX + "I18n";

        public static readonly string CONFIG_PATH = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GkmStatus"), "config.json");

        public const string WINDOWS_THEME_REG_KEY = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        // Fonts
        public const string FONT_REGULAR = RESOURCE_PREFIX + "fonts.IBM_Plex_Sans_JP.IBMPlexSansJP-Regular.ttf";
        public const string FONT_BOLD = RESOURCE_PREFIX + "fonts.IBM_Plex_Sans_JP.IBMPlexSansJP-Bold.ttf";
        public const string FONT_MEDIUM = RESOURCE_PREFIX + "fonts.IBM_Plex_Sans_JP.IBMPlexSansJP-Medium.ttf";

        // Colors
        public static readonly Color COLOR_ERROR = ColorTranslator.FromHtml("#fc5555");
        public static readonly Color COLOR_CONNECT = ColorTranslator.FromHtml("#68b900");
        public static readonly Color COLOR_PAUSE = ColorTranslator.FromHtml("#fc930f");
        public static readonly Color COLOR_PRIMARY = ColorTranslator.FromHtml("#7e87f4");
        public static readonly Color COLOR_DISABLED = Color.FromArgb(60, 63, 65);

        public static readonly Color Default_BackColor = Color.FromArgb(47, 49, 54);
        public static readonly Color Default_ForeColor = Color.White;

        public static readonly Color COLOR_LIGHT_BG = ColorTranslator.FromHtml("#f3f3f4");
        public static readonly Color COLOR_DARK_BG = Color.FromArgb(32, 34, 37);
        public static readonly Color COLOR_OLED_BG = Color.Black;
    }
}
