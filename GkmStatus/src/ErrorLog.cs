using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static GkmStatus.src.AppConstants;

namespace GkmStatus.src
{
    /// <summary>
    /// アプリ内で起きたエラーを、config.json と同じ場所の logs フォルダに日付ごとのファイル（YYYY-MM-DD.log）で記録する。
    /// ログは新しいものから MaxFiles 件だけ残し、新しい日のファイルを作ったときに古いものを削除する。
    /// 記録に失敗してもアプリの動作は止めない。
    /// </summary>
    public static partial class ErrorLog
    {
        /// <summary>残すログファイルの数。</summary>
        public const int MaxFiles = 30;

        /// <summary>同じエラーが続くとき（接続の自動再試行など）は、この間隔に 1 回だけ記録する。</summary>
        public static readonly TimeSpan RepeatInterval = TimeSpan.FromHours(1);

        /// <summary>重複の判定用に覚えておくエラーの数の上限。</summary>
        private const int MaxRememberedErrors = 200;

        public static readonly string DefaultDirectory = Path.Combine(APP_DATA_DIRECTORY, "logs");

        private static readonly object Gate = new();
        private static readonly Dictionary<string, DateTime> LastWritten = [];
        private static readonly UTF8Encoding Utf8NoBom = new(false);

        /// <summary>config.json のパスから、その横の logs フォルダのパスを求める。</summary>
        public static string DirectoryFor(string configPath)
            => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? APP_DATA_DIRECTORY, "logs");

        public static void Write(string context, Exception exception, string? directory = null)
            => Write(context, exception.ToString(), directory);

        public static void Write(string context, string message, string? directory = null)
        {
            Debug.WriteLine($"{context}: {message}");
            try
            {
                lock (Gate)
                {
                    DateTime now = DateTime.Now;
                    string key = context + "\n" + message;
                    if (LastWritten.TryGetValue(key, out var last) && now - last < RepeatInterval)
                        return;
                    ForgetOldEntries(now);
                    LastWritten[key] = now;
                    WriteTo(directory ?? DefaultDirectory, now, context, message);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to write error log: " + e.Message);
            }
        }

        /// <summary>
        /// 重複の判定用に覚えているエラーが増えすぎないようにする（常駐したまま内容の違うエラーが続くと増え続けるため）。
        /// MaxRememberedErrors 件に達したら RepeatInterval を過ぎたものを忘れ、それでも減らなければすべて忘れる。
        /// </summary>
        private static void ForgetOldEntries(DateTime now)
        {
            if (LastWritten.Count < MaxRememberedErrors)
                return;
            foreach (string oldKey in LastWritten.Where(entry => now - entry.Value >= RepeatInterval).Select(entry => entry.Key).ToList())
                LastWritten.Remove(oldKey);
            if (LastWritten.Count >= MaxRememberedErrors)
                LastWritten.Clear();
        }

        /// <summary>1 件を記録する。その日のファイルを新しく作ったときは、古いログを削除する。</summary>
        public static void WriteTo(string directory, DateTime now, string context, string message)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");
            bool isNewFile = !File.Exists(path);
            string line = string.Create(CultureInfo.InvariantCulture,
                $"[{now:yyyy-MM-dd HH:mm:ss}] [v{Application.ProductVersion}] {context}: {message}{Environment.NewLine}");
            File.AppendAllText(path, line, Utf8NoBom);
            if (isNewFile)
                DeleteOldLogs(directory, MaxFiles);
        }

        /// <summary>YYYY-MM-DD.log のうち、新しいものから keep 件を残して削除する。</summary>
        public static void DeleteOldLogs(string directory, int keep)
        {
            var oldFiles = Directory.EnumerateFiles(directory, "*.log")
                .Where(path => LogFileName().IsMatch(Path.GetFileName(path)))
                .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
                .Skip(keep);
            foreach (string path in oldFiles)
            {
                try { File.Delete(path); }
                catch (Exception e) { Debug.WriteLine("Failed to delete old log: " + e.Message); }
            }
        }

        [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}\.log$")]
        private static partial Regex LogFileName();
    }
}
