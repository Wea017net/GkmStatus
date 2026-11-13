using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Text;
using static GkmStatus.src.ui.I18n.Text_List;

namespace GkmStatus.src.ui
{
    /// <summary>プレビューに表示する内容（Discord に送るアクティビティと同じもの）。</summary>
    public sealed record ActivityPreviewData(
        string AppId,
        string AppName,
        string Details,
        string State,
        bool StateIsLink,
        string ImageText,
        IReadOnlyList<string> Buttons,
        TimeSpan Elapsed);

    /// <summary>
    /// Discord のプロフィールに表示されるアクティビティ（「プレイ中」のカード）を再現して表示するウィンドウ。
    /// 寸法と色は Discord の表示に合わせている（アプリのテーマがライト・ダーク・OLED なら、Discord のライト・ダーク・ミッドナイト）。
    /// 数値はすべて CSS px で、DPI に合わせて拡大する。
    /// ホバー時の色の変化・下線・画像の吹き出しは再現するが、クリックしてもリンクは開かない。
    /// </summary>
    public sealed class ActivityPreviewForm : Form
    {
        /// <summary>影（box-shadow: 0 OffsetY Blur rgba(0,0,0,Alpha)）。</summary>
        private sealed record Shadow(float OffsetY, float Blur, double Alpha);

        /// <summary>
        /// テーマごとの Discord の色。半透明の色（Border・ButtonFill など）は、Color の A に不透明度を入れて下の色に重ねる。
        /// </summary>
        private sealed record Palette(
            Color Panel,
            Color Card,
            Color? Border,
            Color HeaderText,
            Color Text,
            Color MenuDots,
            Color Elapsed,
            Color ImagePlaceholder,
            Color ButtonFill,
            Color ButtonHoverFill,
            Color? ButtonBorder,
            Color ButtonText,
            Color TooltipFill,
            Color TooltipRing,
            Shadow? TooltipShadow);

        // カードのホバーとボタンの背景は、どのテーマでも同じ半透明の色を重ねる（OLED のボタンのホバーだけ少し濃い）
        private static readonly Color HoverOverlay = Color.FromArgb(31, 148, 148, 156);        // rgba(148,148,156,.12)
        private static readonly Color ButtonOverlay = Color.FromArgb(31, 148, 148, 156);       // rgba(148,148,156,.12)
        private static readonly Color ButtonHoverOverlay = Color.FromArgb(41, 149, 149, 162);  // rgba(149,149,162,.16)

        private static readonly Palette DarkPalette = new(
            Panel: Color.FromArgb(36, 36, 41),
            Card: Color.FromArgb(44, 45, 50),
            Border: null,
            HeaderText: Color.FromArgb(251, 251, 251),
            Text: Color.FromArgb(239, 239, 241),
            MenuDots: Color.FromArgb(171, 172, 178),
            Elapsed: Color.FromArgb(94, 180, 121),
            ImagePlaceholder: Color.FromArgb(58, 59, 64),
            ButtonFill: ButtonOverlay,
            ButtonHoverFill: ButtonHoverOverlay,
            ButtonBorder: null,
            ButtonText: Color.FromArgb(251, 251, 251),
            TooltipFill: Color.FromArgb(36, 36, 41),
            TooltipRing: Color.FromArgb(64, 148, 148, 156),
            TooltipShadow: null);

        private static readonly Palette LightPalette = new(
            Panel: Color.White,
            Card: Color.White,
            Border: Color.FromArgb(51, 150, 150, 160),       // rgba(150,150,160,.2)
            HeaderText: Color.FromArgb(40, 40, 45),
            Text: Color.FromArgb(46, 46, 52),
            MenuDots: Color.FromArgb(89, 90, 99),
            Elapsed: Color.FromArgb(1, 113, 58),
            ImagePlaceholder: Blend(Color.White, Color.FromArgb(148, 148, 156), 0.2),
            ButtonFill: ButtonOverlay,
            ButtonHoverFill: ButtonHoverOverlay,
            ButtonBorder: Color.FromArgb(51, 150, 150, 160),
            ButtonText: Color.FromArgb(40, 40, 45),
            TooltipFill: Color.White,
            TooltipRing: Color.FromArgb(71, 151, 151, 158),   // rgba(151,151,158,.28)
            TooltipShadow: new Shadow(12, 36, 0.12));

        // Discord の「ミッドナイト」（黒に近いテーマ）。アプリの OLED テーマのときに使う
        private static readonly Palette OledPalette = new(
            Panel: Color.FromArgb(10, 10, 12),
            Card: Color.FromArgb(23, 24, 27),
            Border: null,
            HeaderText: Color.FromArgb(220, 220, 223),
            Text: Color.FromArgb(212, 213, 216),
            MenuDots: Color.FromArgb(150, 151, 158),
            Elapsed: Color.FromArgb(65, 160, 99),
            ImagePlaceholder: Color.FromArgb(40, 41, 45),
            ButtonFill: ButtonOverlay,
            ButtonHoverFill: Color.FromArgb(51, 150, 150, 160),   // rgba(150,150,160,.2)
            ButtonBorder: Color.FromArgb(41, 149, 149, 162),      // rgba(149,149,162,.16)
            ButtonText: Color.FromArgb(220, 220, 223),
            TooltipFill: Color.FromArgb(10, 10, 12),
            TooltipRing: Color.FromArgb(51, 150, 150, 160),
            TooltipShadow: new Shadow(12, 24, 0.24));

        private static Palette PaletteFor(AppTheme theme) => theme switch
        {
            AppTheme.Light => LightPalette,
            AppTheme.OLED => OledPalette,
            _ => DarkPalette,
        };

        // 寸法（CSS px）
        private const int OuterMargin = 16;
        private const int CardWidth = 268;
        private const int CardPadding = 8;
        private const int HeaderHeight = 16;
        private const int SectionGap = 8;
        private const int ImageSize = 60;
        private const int TextLeft = ImageSize + 8;
        private const int TitleHeight = 18;
        private const int LineHeight = 16;
        private const int BadgeGap = 4;
        private const int ButtonsTop = 12;
        private const int ButtonHeight = 32;
        private const int ButtonGap = 8;
        private const int Radius = 8;

        private enum HotSpot { None, Image, Details, State, Button0, Button1 }

        private readonly Func<ActivityPreviewData> _getData;
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private readonly float _k;
        private readonly MixedFont _headerFont, _titleFont, _textFont, _buttonFont, _tooltipFont, _monoFont;

        private Palette _palette;
        private ActivityPreviewData _data;
        private Image? _image;
        private Bitmap? _scaledImage;
        private string? _imageAppId;
        private bool _cardHover;
        private HotSpot _hot;

        /// <param name="theme">Light・Dark・OLED のどれか（Auto は呼び出し側でライトかダークに決めてから渡す）。</param>
        public ActivityPreviewForm(Func<ActivityPreviewData> getData, AppTheme theme, float dpiScale)
        {
            _getData = getData;
            _data = getData();
            _k = dpiScale;
            _palette = PaletteFor(theme);

            // Discord の文字は「gg sans, Hiragino Sans, …, メイリオ, …, Noto Sans, Helvetica Neue, Helvetica, Arial」の指定で、
            // 英数字は gg sans（Discord が読み込むフォント）、日本語は gg sans に無いので Windows ではメイリオで表示される。
            // ここでも文字ごとに分けて、英数字は指定のうちシステムに入っている最初のフォント、日本語はメイリオで描く。
            var installed = InstalledFamilies();
            string latin = FirstInstalled(installed, "gg sans", "Noto Sans", "Helvetica Neue", "Helvetica", "Arial");
            string cjk = FirstInstalled(installed, "Hiragino Sans", "ヒラギノ角ゴ ProN W3", "Hiragino Kaku Gothic ProN", "Meiryo", "メイリオ", "MS PGothic");
            string mono = FirstInstalled(installed, "gg mono", "Source Code Pro", "Consolas", "Lucida Console", "Courier New");

            // 太さ: 見出し・ボタン・吹き出しは 500、アプリ名は 600、その他は 400。
            // メイリオ（と Arial）には 500 が無いので、ブラウザと同じく 500 は標準、600 は太字になる
            _headerFont = new MixedFont(PxFont(latin, 12, FontStyle.Regular), PxFont(cjk, 12, FontStyle.Regular));
            _titleFont = new MixedFont(PxFont(latin, 14, FontStyle.Bold), PxFont(cjk, 14, FontStyle.Bold));
            _textFont = new MixedFont(PxFont(latin, 12, FontStyle.Regular), PxFont(cjk, 12, FontStyle.Regular));
            _buttonFont = new MixedFont(PxFont(latin, 14, FontStyle.Regular), PxFont(cjk, 14, FontStyle.Regular));
            _tooltipFont = new MixedFont(PxFont(latin, 14, FontStyle.Regular), PxFont(cjk, 14, FontStyle.Regular));
            _monoFont = new MixedFont(PxFont(mono, 12, FontStyle.Regular), PxFont(cjk, 12, FontStyle.Regular));

            Text = I18n.T(Preview_Title);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = _palette.Panel;
            DoubleBuffered = true;
            UpdateClientSize();

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _refreshTimer.Tick += (s, e) => RefreshData();
            _refreshTimer.Start();
            LoadImage();
        }

        private bool IsLight => ReferenceEquals(_palette, LightPalette);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ThemeManager.SetTitleBarDarkMode(Handle, !IsLight);
        }

        /// <summary>アプリのテーマが変わったときに、Discord の配色を切り替える。</summary>
        public void SetTheme(AppTheme theme)
        {
            var palette = PaletteFor(theme);
            if (ReferenceEquals(palette, _palette)) return;
            _palette = palette;
            BackColor = palette.Panel;
            if (IsHandleCreated) ThemeManager.SetTitleBarDarkMode(Handle, !IsLight);
            Invalidate();
        }

        /// <summary>メイン画面の入力内容を読み直して描き直す（経過時間もここで進む）。</summary>
        public void RefreshData()
        {
            _data = _getData();
            if (_data.AppId != _imageAppId) LoadImage();
            UpdateClientSize();
            UpdateHotSpot(PointToClient(Cursor.Position));
            Invalidate();
        }

        private void LoadImage()
        {
            string appId = _data.AppId;
            _imageAppId = appId;
            _ = DiscordAppImage.GetAsync(appId, "app").ContinueWith(task =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(() =>
                {
                    if (IsDisposed || _imageAppId != appId) return;
                    _image = task.Result;
                    _scaledImage?.Dispose();
                    _scaledImage = null;
                    Invalidate();
                });
            }, TaskScheduler.Default);
        }

        // ---- レイアウト（CSS px） ----

        private int DetailLineCount => (_data.Details.Length > 0 ? 1 : 0) + (_data.State.Length > 0 ? 1 : 0);
        private int TextColumnHeight => TitleHeight + LineHeight * DetailLineCount + BadgeGap + LineHeight;
        private int BodyHeight => Math.Max(ImageSize, TextColumnHeight);
        private int BodyTop => CardPadding + HeaderHeight + SectionGap;
        private int ButtonsY => BodyTop + BodyHeight + ButtonsTop;
        private int StateTop => TitleHeight + (_data.Details.Length > 0 ? LineHeight : 0);

        private int CardHeight
        {
            get
            {
                int h = BodyTop + BodyHeight;
                int n = _data.Buttons.Count;
                if (n > 0) h += ButtonsTop + n * ButtonHeight + (n - 1) * ButtonGap;
                return h + CardPadding;
            }
        }

        private void UpdateClientSize()
        {
            var size = new Size(Px(CardWidth + OuterMargin * 2), Px(CardHeight + OuterMargin * 2));
            if (ClientSize != size) ClientSize = size;
        }

        /// <summary>カード内の位置（CSS px）を、ウィンドウ上の矩形（実ピクセル）にする。</summary>
        private Rectangle CardRect(float x, float y, float w, float h)
            => Rectangle.Round(new RectangleF((OuterMargin + x) * _k, (OuterMargin + y) * _k, w * _k, h * _k));

        private Rectangle CardBounds => CardRect(0, 0, CardWidth, CardHeight);
        private Rectangle ImageBounds => CardRect(CardPadding, BodyTop, ImageSize, ImageSize);
        private Rectangle TextArea(int top, int height) => CardRect(CardPadding + TextLeft, BodyTop + top, CardWidth - CardPadding * 2 - TextLeft, height);
        private Rectangle DetailsBounds => TextLineBounds(_data.Details, TextArea(TitleHeight, LineHeight));
        private Rectangle StateBounds => TextLineBounds(_data.State, TextArea(StateTop, LineHeight));
        private Rectangle ButtonBounds(int index)
            => CardRect(CardPadding, ButtonsY + index * (ButtonHeight + ButtonGap), CardWidth - CardPadding * 2, ButtonHeight);

        /// <summary>テキスト 1 行の範囲（ホバー判定に使うので、文字の幅だけにする）。</summary>
        private Rectangle TextLineBounds(string text, Rectangle area)
            => new(area.X, area.Y, LayoutLine(text, _textFont, area.Width).Sum(run => run.Width), area.Height);

        // ---- マウス ----

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            UpdateHotSpot(e.Location);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            UpdateHotSpot(new Point(-1, -1));
        }

        private void UpdateHotSpot(Point p)
        {
            bool cardHover = CardBounds.Contains(p);
            HotSpot hot = HotSpot.None;
            if (ImageBounds.Contains(p)) hot = HotSpot.Image;
            else if (_data.Details.Length > 0 && DetailsBounds.Contains(p)) hot = HotSpot.Details;
            else if (_data.State.Length > 0 && _data.StateIsLink && StateBounds.Contains(p)) hot = HotSpot.State;
            else if (_data.Buttons.Count > 0 && ButtonBounds(0).Contains(p)) hot = HotSpot.Button0;
            else if (_data.Buttons.Count > 1 && ButtonBounds(1).Contains(p)) hot = HotSpot.Button1;

            // Discord ではカード全体がクリックできる見た目（ポインター）になる
            Cursor = cardHover ? Cursors.Hand : Cursors.Default;
            if (cardHover == _cardHover && hot == _hot) return;
            _cardHover = cardHover;
            _hot = hot;
            Invalidate();
        }

        // ---- 描画 ----

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var palette = _palette;
            g.Clear(palette.Panel);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color card = _cardHover ? Over(palette.Panel, HoverOverlay) : palette.Card;
            FillRounded(g, CardBounds, card, palette.Border);

            // ヘッダー「プレイ中：」と右端の「…」
            DrawLine(g, I18n.T(Preview_Playing), _headerFont, CardRect(CardPadding, CardPadding, 200, HeaderHeight), palette.HeaderText);
            using (var dots = new SolidBrush(palette.MenuDots))
            {
                foreach (float cx in new[] { 3.5f, 8f, 12.5f })
                {
                    float x = (OuterMargin + CardWidth - CardPadding - 20 + 4 + cx) * _k, y = (OuterMargin + CardPadding + 8) * _k, r = 1.5f * _k;
                    g.FillEllipse(dots, x - r, y - r, r * 2, r * 2);
                }
            }

            DrawImage(g);

            // アプリ名・Details・State（Details と リンク付きの State はホバーで下線）
            DrawLine(g, _data.AppName, _titleFont, TextArea(0, TitleHeight), palette.Text);
            if (_data.Details.Length > 0)
                DrawLine(g, _data.Details, _textFont, TextArea(TitleHeight, LineHeight), palette.Text, underline: _hot == HotSpot.Details);
            if (_data.State.Length > 0)
                DrawLine(g, _data.State, _textFont, TextArea(StateTop, LineHeight), palette.Text, underline: _hot == HotSpot.State);

            // 経過時間（ゲームコントローラーのアイコン + 時間）
            int badgeTop = BodyTop + TitleHeight + LineHeight * DetailLineCount + BadgeGap;
            DrawControllerIcon(g, CardRect(CardPadding + TextLeft, badgeTop + 2, 12, 12), palette.Elapsed, card);
            DrawLine(g, FormatElapsed(_data.Elapsed), _monoFont, CardRect(CardPadding + TextLeft + 16, badgeTop, 100, LineHeight), palette.Elapsed);

            // ボタン
            for (int i = 0; i < _data.Buttons.Count && i < 2; i++)
            {
                bool hot = _hot == (i == 0 ? HotSpot.Button0 : HotSpot.Button1);
                Color fill = Over(card, hot ? palette.ButtonHoverFill : palette.ButtonFill);
                var bounds = ButtonBounds(i);
                FillRounded(g, bounds, fill, palette.ButtonBorder);
                DrawLine(g, _data.Buttons[i], _buttonFont, Rectangle.Inflate(bounds, -Px(12), 0), palette.ButtonText, center: true);
            }

            if (_hot == HotSpot.Image && _data.ImageText.Length > 0)
                DrawTooltip(g, _data.ImageText);
        }

        /// <summary>角丸の矩形を塗り、枠線があれば 1 ピクセルの線で縁取る（Discord の 1px の枠は画面の 1 ピクセルになる）。</summary>
        private void FillRounded(Graphics g, Rectangle bounds, Color fill, Color? border)
        {
            using (var path = RoundedRect(bounds, Px(Radius)))
            using (var brush = new SolidBrush(fill))
                g.FillPath(brush, path);
            if (border is not Color color) return;
            var inner = RectangleF.Inflate(bounds, -0.5f, -0.5f);
            using var borderPath = RoundedRect(inner, Px(Radius) - 0.5f);
            using var pen = new Pen(color, 1f);
            g.DrawPath(pen, borderPath);
        }

        private void DrawImage(Graphics g)
        {
            var bounds = ImageBounds;
            using var path = RoundedRect(bounds, Px(Radius));
            if (_image is null)
            {
                using var placeholder = new SolidBrush(_palette.ImagePlaceholder);
                g.FillPath(placeholder, path);
                return;
            }

            // 角丸の縁をなめらかにするため、縮小した画像をテクスチャにして角丸の形で塗る
            if (_scaledImage is null || _scaledImage.Size != bounds.Size)
            {
                _scaledImage?.Dispose();
                _scaledImage = new Bitmap(bounds.Width, bounds.Height);
                using var sg = Graphics.FromImage(_scaledImage);
                sg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                sg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                sg.DrawImage(_image, new Rectangle(Point.Empty, bounds.Size));
            }
            using var texture = new TextureBrush(_scaledImage, WrapMode.Clamp);
            texture.TranslateTransform(bounds.X, bounds.Y);
            g.FillPath(texture, path);
        }

        /// <summary>
        /// 経過時間の横のゲームコントローラーのアイコン（このアプリ用に描いた形）。
        /// 横長の本体の左右下にグリップがあり、左に十字キー、右に 2 つのボタンの穴がある。
        /// </summary>
        private static void DrawControllerIcon(Graphics g, Rectangle box, Color color, Color background)
        {
            float s = box.Width / 12f;
            PointF P(float x, float y) => new(box.X + x * s, box.Y + y * s);

            using (var body = RoundedRect(RectangleF.FromLTRB(P(0.4f, 1.8f).X, P(0.4f, 1.8f).Y, P(11.6f, 8.2f).X, P(11.6f, 8.2f).Y), 2.6f * s))
            using (var brush = new SolidBrush(color))
            {
                g.FillPath(brush, body);
                // 左右のグリップ
                g.FillEllipse(brush, new RectangleF(P(0.3f, 5.4f), new SizeF(4.4f * s, 4.8f * s)));
                g.FillEllipse(brush, new RectangleF(P(7.3f, 5.4f), new SizeF(4.4f * s, 4.8f * s)));
            }

            using var hole = new SolidBrush(background);
            // 十字キー
            g.FillRectangle(hole, new RectangleF(P(2.0f, 4.55f), new SizeF(3.0f * s, 0.9f * s)));
            g.FillRectangle(hole, new RectangleF(P(3.05f, 3.5f), new SizeF(0.9f * s, 3.0f * s)));
            // ボタン
            float r = 0.62f * s;
            foreach (var c in new[] { P(8.2f, 4.2f), P(9.5f, 5.6f) })
                g.FillEllipse(hole, c.X - r, c.Y - r, r * 2, r * 2);
        }

        /// <summary>画像にマウスを乗せたときの吹き出し（LargeImageText）。画像の上に中央そろえで出す。</summary>
        private void DrawTooltip(Graphics g, string text)
        {
            var palette = _palette;
            var image = ImageBounds;
            int textWidth = LayoutLine(text, _tooltipFont, int.MaxValue).Sum(run => run.Width);
            int width = textWidth + Px(24), height = Px(34);
            int x = image.X + image.Width / 2 - width / 2;
            x = Math.Clamp(x, Px(4), Math.Max(Px(4), ClientSize.Width - width - Px(4)));
            int y = Math.Max(Px(2), image.Y - Px(10) - height);
            var bounds = new Rectangle(x, y, width, height);

            if (palette.TooltipShadow is Shadow shadow) DrawShadow(g, bounds, shadow);
            using (var path = RoundedRect(bounds, Px(Radius)))
            using (var fill = new SolidBrush(palette.TooltipFill))
                g.FillPath(fill, path);
            // 内側の 1px の縁取り（box-shadow: inset 0 0 0 1px）
            float ring = Math.Max(1f, _k);
            using (var ringPath = RoundedRect(RectangleF.Inflate(bounds, -ring / 2, -ring / 2), Px(Radius) - ring / 2))
            using (var pen = new Pen(palette.TooltipRing, ring))
                g.DrawPath(pen, ringPath);

            DrawLine(g, text, _tooltipFont, bounds, palette.Text, center: true);
        }

        /// <summary>吹き出しの影。ぼかしは大きさを変えた薄い影を重ねて近似する。</summary>
        private void DrawShadow(Graphics g, Rectangle bounds, Shadow shadow)
        {
            const int steps = 12;
            float blur = shadow.Blur / 2 * _k;
            var basis = new RectangleF(bounds.X, bounds.Y + shadow.OffsetY * _k, bounds.Width, bounds.Height);
            using var brush = new SolidBrush(Color.FromArgb((int)Math.Ceiling(255 * shadow.Alpha / steps), 0, 0, 0));
            for (int i = 0; i < steps; i++)
            {
                float grow = blur * (1 - 2f * (i + 0.5f) / steps);
                var r = RectangleF.Inflate(basis, grow, grow);
                if (r.Width <= 0 || r.Height <= 0) continue;
                using var path = RoundedRect(r, Math.Max(0.5f, Px(Radius) + grow));
                g.FillPath(brush, path);
            }
        }

        // ---- 文字（英数字と日本語でフォントを分ける） ----

        /// <summary>同じ大きさ・太さの、英数字用と日本語用のフォントの組。</summary>
        private sealed class MixedFont(Font latin, Font cjk) : IDisposable
        {
            public Font Latin { get; } = latin;
            public Font Cjk { get; } = cjk;

            /// <summary>
            /// その文字を英数字用のフォントで描くか。gg sans などの欧文フォントにある文字（ラテン文字・記号・一般句読点）は英数字用、
            /// それ以外（かな・漢字・全角記号など）は日本語用。ブラウザのフォントの代替と同じ分け方。
            /// </summary>
            public Font For(string textElement)
            {
                Rune.DecodeFromUtf16(textElement, out Rune rune, out _);
                return rune.Value <= 0x206F ? Latin : Cjk;
            }

            public void Dispose()
            {
                Latin.Dispose();
                Cjk.Dispose();
            }
        }

        private readonly record struct TextRun(string Text, Font Font, int Width);

        private const TextFormatFlags RunFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        private static int MeasureRun(string text, Font font)
            => text.Length == 0 ? 0 : TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), RunFlags).Width;

        /// <summary>文字列をフォントごとのまとまりに分け、幅に入らなければ末尾を削って「…」を付ける（text-overflow: ellipsis）。</summary>
        private static List<TextRun> LayoutLine(string text, MixedFont font, int maxWidth)
        {
            var pieces = new List<(string Text, Font Font)>();
            var buffer = new StringBuilder();
            Font? current = null;
            var elements = StringInfo.GetTextElementEnumerator(text);
            while (elements.MoveNext())
            {
                string element = elements.GetTextElement();
                Font f = font.For(element);
                if (current is not null && f != current)
                {
                    pieces.Add((buffer.ToString(), current));
                    buffer.Clear();
                }
                current = f;
                buffer.Append(element);
            }
            if (current is not null) pieces.Add((buffer.ToString(), current));

            var runs = pieces.Select(p => new TextRun(p.Text, p.Font, MeasureRun(p.Text, p.Font))).ToList();
            if (runs.Sum(run => run.Width) <= maxWidth) return runs;

            const string ellipsis = "…";
            int ellipsisWidth = MeasureRun(ellipsis, font.Latin);
            var fitted = new List<TextRun>();
            int used = 0;
            foreach (var run in runs)
            {
                if (used + run.Width + ellipsisWidth <= maxWidth)
                {
                    fitted.Add(run);
                    used += run.Width;
                    continue;
                }
                // この塊の途中で切る。入るところまで 1 文字ずつ伸ばす
                string fit = "";
                var runElements = StringInfo.GetTextElementEnumerator(run.Text);
                var prefix = new StringBuilder();
                int fitWidth = 0;
                while (runElements.MoveNext())
                {
                    prefix.Append(runElements.GetTextElement());
                    int w = MeasureRun(prefix.ToString(), run.Font);
                    if (used + w + ellipsisWidth > maxWidth) break;
                    fit = prefix.ToString();
                    fitWidth = w;
                }
                if (fit.Length > 0) fitted.Add(new TextRun(fit, run.Font, fitWidth));
                break;
            }
            fitted.Add(new TextRun(ellipsis, font.Latin, ellipsisWidth));
            return fitted;
        }

        /// <summary>
        /// 1 行を描く。行の高さの中で、英数字用フォントの行に合わせたベースラインにすべての文字をそろえる
        /// （ブラウザと同じく、日本語の文字も英数字のベースラインに乗る）。
        /// </summary>
        private void DrawLine(Graphics g, string text, MixedFont font, Rectangle area, Color color, bool center = false, bool underline = false)
        {
            var runs = LayoutLine(text, font, area.Width);
            int total = runs.Sum(run => run.Width);
            int left = center ? area.X + (area.Width - total) / 2 : area.X;

            float ascent = CellAscent(font.Latin), descent = CellDescent(font.Latin);
            int baseline = (int)Math.Round(area.Y + (area.Height - (ascent + descent)) / 2 + ascent);

            int x = left;
            foreach (var run in runs)
            {
                int top = baseline - (int)Math.Round(CellAscent(run.Font));
                TextRenderer.DrawText(g, run.Text, run.Font, new Point(x, top), color, RunFlags);
                x += run.Width;
            }

            if (underline && total > 0)
            {
                // 欧文フォントの下線の位置（ベースラインの 0.1em 下）と太さ（約 0.07em）
                float size = font.Latin.Size;
                int y = baseline + Math.Max(1, (int)Math.Round(size * 0.1f));
                int thickness = Math.Max(1, (int)Math.Round(size * 0.07f));
                using var brush = new SolidBrush(color);
                var smoothing = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.None;
                g.FillRectangle(brush, left, y, total, thickness);
                g.SmoothingMode = smoothing;
            }
        }

        private static float CellAscent(Font font)
            => font.Size * font.FontFamily.GetCellAscent(font.Style) / font.FontFamily.GetEmHeight(font.Style);

        private static float CellDescent(Font font)
            => font.Size * font.FontFamily.GetCellDescent(font.Style) / font.FontFamily.GetEmHeight(font.Style);

        // ---- 補助 ----

        /// <summary>Discord と同じ表記（1 時間未満は「分:秒」、それ以上は「時:分:秒」）。</summary>
        public static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
            return elapsed.TotalHours >= 1
                ? string.Create(CultureInfo.InvariantCulture, $"{(int)elapsed.TotalHours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}")
                : string.Create(CultureInfo.InvariantCulture, $"{elapsed.Minutes}:{elapsed.Seconds:D2}");
        }

        private int Px(float cssPx) => (int)Math.Round(cssPx * _k);

        private Font PxFont(string family, float cssPx, FontStyle style) => new(family, cssPx * _k, style, GraphicsUnit.Pixel);

        private static HashSet<string> InstalledFamilies()
        {
            using var installed = new InstalledFontCollection();
            return installed.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string FirstInstalled(HashSet<string> installed, params string[] families)
            => families.FirstOrDefault(installed.Contains) ?? SystemFonts.MessageBoxFont?.FontFamily.Name ?? "Segoe UI";

        /// <summary>半透明の色（A が不透明度）を不透明の色に重ねた色。</summary>
        private static Color Over(Color under, Color over) => Blend(under, over, over.A / 255.0);

        private static Color Blend(Color under, Color over, double alpha)
            => Color.FromArgb(
                (int)Math.Round(under.R * (1 - alpha) + over.R * alpha),
                (int)Math.Round(under.G * (1 - alpha) + over.G * alpha),
                (int)Math.Round(under.B * (1 - alpha) + over.B * alpha));

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer.Dispose();
                _scaledImage?.Dispose();
                foreach (var font in new[] { _headerFont, _titleFont, _textFont, _buttonFont, _tooltipFont, _monoFont })
                    font.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
