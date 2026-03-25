using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static GkmStatus.src.AppConstants;
using GkmStatus.src.ui;

namespace GkmStatus.src
{
	public enum PresenceStateType
	{
		None,
		PID,
		Idol,
		Producing,
		Custom
	}

	public enum PresenceDetailsType
	{
		None,
		PName,
		PLv,
		Both
	}

	public enum PresenceButtonMode
	{
		None,
		Store,
		App,
		Custom
	}

	public static class StartupManager
	{
		public static bool IsRunAtStartup()
		{
			try
			{
				using RegistryKey? key = Registry.CurrentUser.OpenSubKey(REG_RUN_KEY, false);
				return key?.GetValue(APP_NAME) != null;
			}
			catch { return false; }
		}

		public static void SetRunAtStartup(bool run)
		{
			try
			{
				using RegistryKey? key = Registry.CurrentUser.OpenSubKey(REG_RUN_KEY, true);
				if (key != null)
				{
					if (run) key.SetValue(APP_NAME, Application.ExecutablePath);
					else key.DeleteValue(APP_NAME, false);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("スタートアップ設定の変更に失敗しました: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		public static bool OpenUrl(string url)
		{
			try
			{
				Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
				return true;
			}
			catch
			{
				return false;
			}
		}
	}

	public static class AppSettingsHelper
	{
		private static readonly Regex _japaneseRegex = new(@"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]", RegexOptions.Compiled);

		public static Regex JapaneseRegex() => _japaneseRegex;

		public static PresenceStateType GetStateType(int i) => Enum.IsDefined(typeof(PresenceStateType), i) ? (PresenceStateType)i : PresenceStateType.Producing;
		public static PresenceStateType GetStateType(string? s) => Enum.TryParse<PresenceStateType>(s, out var t) ? t : PresenceStateType.Producing;
		public static string GetStateString(PresenceStateType t) => t.ToString();
		public static int GetStateIndex(PresenceStateType t) => (int)t;
		public static string GetStateString(int i) => GetStateString(GetStateType(i));
		public static int GetStateIndex(string s) => GetStateIndex(GetStateType(s));

		public static PresenceDetailsType GetDetailsType(int i) => Enum.IsDefined(typeof(PresenceDetailsType), i) ? (PresenceDetailsType)i : PresenceDetailsType.Both;
		public static PresenceDetailsType GetDetailsType(string? s) => Enum.TryParse<PresenceDetailsType>(s, out var t) ? t : PresenceDetailsType.Both;
		public static string GetDetailsString(PresenceDetailsType t) => t.ToString();
		public static int GetDetailsIndex(PresenceDetailsType t) => (int)t;
		public static string GetDetailsString(int i) => GetDetailsString(GetDetailsType(i));
		public static int GetDetailsIndex(string s) => GetDetailsIndex(GetDetailsType(s));

		public static PresenceButtonMode GetButtonMode(int i) => Enum.IsDefined(typeof(PresenceButtonMode), i) ? (PresenceButtonMode)i : PresenceButtonMode.None;
		public static PresenceButtonMode GetButtonMode(string? s) => Enum.TryParse<PresenceButtonMode>(s, out var t) ? t : PresenceButtonMode.None;
		public static string GetButtonModeString(PresenceButtonMode t) => t.ToString();
		public static int GetButtonModeIndex(PresenceButtonMode t) => (int)t;
		public static string GetButtonModeString(int i) => GetButtonModeString(GetButtonMode(i));
		public static int GetButtonModeIndex(string s) => GetButtonModeIndex(GetButtonMode(s));

		public static AppTheme GetTheme(int i) => Enum.IsDefined(typeof(AppTheme), i) ? (AppTheme)i : AppTheme.Auto;
		public static AppTheme GetTheme(string? s) => Enum.TryParse<AppTheme>(s, out var t) ? t : AppTheme.Auto;
		public static string GetThemeString(AppTheme t) => t.ToString();
		public static string GetThemeString(int i) => GetThemeString(GetTheme(i));
		public static int GetThemeIndex(string s) => GetThemeIndex(GetTheme(s));
		public static int GetThemeIndex(AppTheme t) => (int)t;

		public static I18n.Language GetLanguage(int i) => i == 1 ? I18n.Language.English : I18n.Language.Japanese;
		public static I18n.Language GetLanguage(string? s) => (s == "en" || s == "English") ? I18n.Language.English : I18n.Language.Japanese;
		public static string GetLangString(I18n.Language l) => l == I18n.Language.English ? "en" : "ja";
		public static string GetLangString(int i) => GetLangString(GetLanguage(i));

		public static bool IsValidRpcButtonUrl(string? url) => !string.IsNullOrEmpty(url)
			&& (url.StartsWith("http://") || url.StartsWith("https://"))
			&& !_japaneseRegex.IsMatch(url);
	}
}
