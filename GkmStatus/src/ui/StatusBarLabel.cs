using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace GkmStatus.src.ui
{
    /// <summary>
    /// 画面下端のステータス欄。翻訳文の改行（「未接続\n（自動接続が有効）」など）を 1 行にまとめて表示する。
    /// 全角のかっこの前は空白を入れずにつなぎ、それ以外は空白 1 つでつなぐ。入りきらないときは末尾を「…」で省略する。
    /// </summary>
    internal sealed partial class StatusBarLabel : Label
    {
        public StatusBarLabel()
        {
            AutoSize = false;
            AutoEllipsis = true;
            TextAlign = ContentAlignment.MiddleLeft;
        }

        [AllowNull]
        public override string Text
        {
            get => base.Text;
            set => base.Text = ToSingleLine(value);
        }

        public static string ToSingleLine(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string joined = LineBreakBeforeFullWidthParen().Replace(text, "（");
            return Whitespace().Replace(joined.Replace("\r\n", " ").Replace('\n', ' '), " ").Trim();
        }

        [GeneratedRegex(@"\s*\r?\n\s*（")]
        private static partial Regex LineBreakBeforeFullWidthParen();

        [GeneratedRegex(@"[ \t]{2,}")]
        private static partial Regex Whitespace();
    }
}
