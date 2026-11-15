using System.Drawing.Drawing2D;
using System.Runtime.Versioning;

namespace GkmStatus.src.ui
{
    /// <summary>
    /// メイン画面の各見出し（「1行目の情報 (Details)」など）をオレンジのグラデーションの帯にし、
    /// 見出しとその下の項目をまとめて角丸のカードで囲む。
    /// コントロールは作り直さず、フォームの背景にカードを描き、項目を縦方向にずらす。
    /// 見出しのラベルは隠して、その文字を帯の中に描く。
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class SectionCards
    {
        // 見出しの帯
        private static readonly Color BandStart = ColorTranslator.FromHtml("#ffaa21");
        private static readonly Color BandEnd = ColorTranslator.FromHtml("#ffc857");
        private static readonly Color BandSparkleRight = ColorTranslator.FromHtml("#ffce62");
        private static readonly Color BandSparkleLeft = ColorTranslator.FromHtml("#ffdcbc");

        // カード。ダーク/OLED は背景との差が小さいと境目が見えないので、少し明るい面に細い枠線を付ける
        private static readonly Color LightCard = ColorTranslator.FromHtml("#fefefe");
        private static readonly Color DarkCard = ColorTranslator.FromHtml("#2c2f34");
        private static readonly Color DarkCardBorder = ColorTranslator.FromHtml("#4a4e56");
        private static readonly Color OledCard = ColorTranslator.FromHtml("#16171a");
        private static readonly Color OledCardBorder = ColorTranslator.FromHtml("#3a3c42");

        private sealed record Section(Label Header, Rectangle Card);

        private readonly Form _form;
        private readonly Func<int, int> _s;
        private readonly int _bandHeight;
        private readonly List<Section> _sections = [];
        private readonly List<AccentComboBox> _combos = [];

        private SectionCards(Form form, Func<int, int> s)
        {
            _form = form;
            _s = s;
            _bandHeight = s(28);
        }

        private static bool IsLight(Color formBack) => formBack.GetBrightness() > 0.5f;
        private static bool IsOled(Color formBack) => formBack.R == 0 && formBack.G == 0 && formBack.B == 0;

        /// <summary>カードの背景色。ライトは #fefefe、ダーク/OLED は背景より明るい色。</summary>
        public static Color CardColor(Color formBack)
            => IsLight(formBack) ? LightCard : IsOled(formBack) ? OledCard : DarkCard;

        /// <summary>カードの枠線の色。ライトは枠線なし（null）。</summary>
        private static Color? CardBorderColor(Color formBack)
            => IsLight(formBack) ? null : IsOled(formBack) ? OledCardBorder : DarkCardBorder;

        /// <summary>
        /// headers（上から順）ごとに、そのすぐ下から次の見出しまで（最後は bottomLimit まで）にあるコントロールを 1 つのカードにまとめる。
        /// 呼ぶ時点では、見出しの下 headerToContent の位置から項目が並んでいる前提。
        /// </summary>
        public static void Apply(Form form, IReadOnlyList<Label> headers, int top, int headerToContent, int bottomLimit, Func<int, int> s)
        {
            var cards = new SectionCards(form, s);
            cards.Layout(headers, top, headerToContent, bottomLimit);
            form.Paint += cards.OnFormPaint;
            form.BackColorChanged += (_, _) => cards.UpdateColors();
            cards.UpdateColors();
        }

        private void Layout(IReadOnlyList<Label> headers, int top, int headerToContent, int bottomLimit)
        {
            int left = _s(10), right = _form.ClientSize.Width - _s(10);
            // padding はカード内の上下の余白（左右の余白＝カードの端から入力欄まで と同じ）、gap はカード同士の間隔
            int padding = _s(10), gap = _s(6);
            int cursor = top;

            // 移動する前の位置で、各見出しに属するコントロールを決めておく
            var groups = new List<List<Control>>();
            for (int i = 0; i < headers.Count; i++)
            {
                int from = headers[i].Top, to = i + 1 < headers.Count ? headers[i + 1].Top : bottomLimit;
                groups.Add([.. _form.Controls.Cast<Control>().Where(c => c != headers[i] && c is not MenuStrip && c.Top > from && c.Top < to)]);
            }

            for (int i = 0; i < headers.Count; i++)
            {
                Label header = headers[i];
                int dy = cursor + _bandHeight + padding - (header.Top + headerToContent);
                int contentBottom = cursor + _bandHeight + padding;
                foreach (Control c in groups[i])
                {
                    c.Top += dy;
                    // カードの高さは入力欄・ボタンで決める（ラベルは一時的な案内や警告があり、カードの外にはみ出してよい）
                    if (c is not Label) contentBottom = Math.Max(contentBottom, c.Bottom);
                    // カード上の文字はカードの色の上に描く
                    if (c is Label label) label.BackColor = Color.Transparent;
                    if (c is AccentComboBox combo) _combos.Add(combo);
                }
                var card = Rectangle.FromLTRB(left, cursor, right, contentBottom + padding);
                _sections.Add(new Section(header, card));

                header.Visible = false; // 文字は帯の中に描く（言語の切り替えで変わるので描くたびに読む）
                header.TextChanged += (_, _) => _form.Invalidate(card);
                cursor = card.Bottom + gap;
            }
        }

        private void UpdateColors()
        {
            Color card = CardColor(_form.BackColor);
            foreach (AccentComboBox combo in _combos)
            {
                combo.OuterBackColor = card;
                combo.Invalidate();
            }
            _form.Invalidate();
        }

        private void OnFormPaint(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            var oldSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color cardColor = CardColor(_form.BackColor);
            Color? borderColor = CardBorderColor(_form.BackColor);
            foreach (Section section in _sections)
            {
                if (!e.ClipRectangle.IntersectsWith(section.Card)) continue;
                DrawCard(g, section, cardColor, borderColor);
            }
            g.SmoothingMode = oldSmoothing;
        }

        private void DrawCard(Graphics g, Section section, Color cardColor, Color? borderColor)
        {
            Rectangle card = section.Card;
            using GraphicsPath path = CardPath(card);
            using (var brush = new SolidBrush(cardColor)) g.FillPath(brush, path);
            // 枠線は帯より先に描く（帯の部分は帯で上書きされ、項目の部分だけ縁取られる）
            if (borderColor is Color border)
            {
                using var pen = new Pen(border, 1f);
                using GraphicsPath edge = CardPath(new Rectangle(card.Left, card.Top, card.Width - 1, card.Height - 1));
                g.DrawPath(pen, edge);
            }

            // 見出しの帯（カードの角丸で切り抜く）
            var band = new Rectangle(card.Left, card.Top, card.Width, _bandHeight);
            GraphicsState state = g.Save();
            g.SetClip(path, CombineMode.Intersect);
            g.SetClip(band, CombineMode.Intersect);
            using (var gradient = new LinearGradientBrush(new Rectangle(band.Left - 1, band.Top, band.Width + 2, band.Height), BandStart, BandEnd, LinearGradientMode.Horizontal))
                g.FillRectangle(gradient, band);

            // 右端の斜めに明るい部分と、その中のきらめき
            float slant = band.Height * 1.2f;
            float lightLeft = band.Right - band.Width * 0.2f;
            using (var light = new SolidBrush(Color.FromArgb(110, Color.White)))
            {
                g.FillPolygon(light,
                [
                    new PointF(lightLeft, band.Top), new PointF(band.Right, band.Top),
                    new PointF(band.Right, band.Bottom), new PointF(lightLeft - slant, band.Bottom),
                ]);
            }
            DrawSparkleWithRays(g, new PointF(band.Right - band.Height * 1.1f, band.Top + band.Height / 2f), band.Height * 0.32f, BandSparkleRight);
            g.Restore(state);

            // 左端のきらめきと見出しの文字
            float leftSparkleX = band.Left + _s(13);
            FillSparkle(g, new PointF(leftSparkleX, band.Top + band.Height / 2f), band.Height * 0.24f, BandSparkleLeft);
            Label header = section.Header;
            var textBounds = Rectangle.FromLTRB(band.Left + _s(24), band.Top, band.Right - _s(8), band.Bottom);
            TextRenderer.DrawText(g, header.Text, header.Font, textBounds, Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        // 4 方向にとがった星（きらめき）
        private static void FillSparkle(Graphics g, PointF center, float radius, Color color)
        {
            float inner = radius * 0.22f;
            var points = new PointF[8];
            for (int i = 0; i < 8; i++)
            {
                double angle = Math.PI / 4 * i - Math.PI / 2;
                float r = i % 2 == 0 ? radius : inner;
                points[i] = new PointF(center.X + (float)Math.Cos(angle) * r, center.Y + (float)Math.Sin(angle) * r);
            }
            using var brush = new SolidBrush(color);
            g.FillPolygon(brush, points);
        }

        // きらめきと、その周りの斜めの短い線
        private static void DrawSparkleWithRays(Graphics g, PointF center, float radius, Color color)
        {
            FillSparkle(g, center, radius, color);
            using var pen = new Pen(color, Math.Max(1f, radius * 0.16f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (int i = 0; i < 4; i++)
            {
                double angle = Math.PI / 4 + Math.PI / 2 * i;
                float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
                g.DrawLine(pen, center.X + cos * radius * 1.15f, center.Y + sin * radius * 1.15f,
                                center.X + cos * radius * 1.65f, center.Y + sin * radius * 1.65f);
            }
        }

        // カードの形: 左上と右下は小さく、右上と左下は大きく丸める
        private GraphicsPath CardPath(Rectangle r) => RoundedRect(r, _s(3), _s(12), _s(3), _s(10));

        private static GraphicsPath RoundedRect(Rectangle r, int topLeft, int topRight, int bottomRight, int bottomLeft)
        {
            var path = new GraphicsPath();
            void Corner(int x, int y, int radius, float startAngle)
            {
                if (radius <= 0) path.AddLine(x, y, x, y);
                else path.AddArc(x, y, radius * 2, radius * 2, startAngle, 90);
            }
            Corner(r.Left, r.Top, topLeft, 180);
            Corner(topRight > 0 ? r.Right - topRight * 2 : r.Right, r.Top, topRight, 270);
            Corner(bottomRight > 0 ? r.Right - bottomRight * 2 : r.Right, bottomRight > 0 ? r.Bottom - bottomRight * 2 : r.Bottom, bottomRight, 0);
            Corner(r.Left, bottomLeft > 0 ? r.Bottom - bottomLeft * 2 : r.Bottom, bottomLeft, 90);
            path.CloseFigure();
            return path;
        }
    }
}
