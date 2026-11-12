using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace GkmStatus.src.ui
{
    /// <summary>
    /// 標準の ToolStripProfessionalRenderer をベースに、次の 2 点だけを変える（メインウィンドウのメニューバー用）。
    /// ・サブメニュー矢印の色を項目の文字色に合わせる（既定ではダーク背景でも黒で描かれて見えないため）
    /// ・チェックマークの形を独自画像（Resources/menu_check.png）にし、項目の文字色で塗って描く
    /// ・択一の項目（RadioMenuItem）は、チェックの代わりにラジオボタンの画像（Resources/menu_radio.png）を描く
    /// チェック欄の背景・枠は標準どおり ColorTable の色で描く（ラジオは背景・枠なしで円だけ描く）。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class ThemedProfessionalRenderer(ProfessionalColorTable colorTable) : ToolStripProfessionalRenderer(colorTable)
    {
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            if (e.Item is { Enabled: true } item) e.ArrowColor = item.ForeColor;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            // メニューを開いたまま隣の項目へ移ると、新しい項目は「選択済み・未展開」の状態で一瞬描かれてから
            // ドロップダウンが開く。この間にホバー色が出てちらつくので、切り替え中は何も描かない。
            if (e.Item is ToolStripMenuItem { Selected: true, Pressed: false, HasDropDown: true } item
                && item.Owner is MenuStrip menuStrip
                && MenuBarSwitchTracker.IsSwitching(menuStrip))
            {
                return;
            }
            base.OnRenderMenuItemBackground(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Item is RadioMenuItem radio)
            {
                MenuRadioGlyph.Draw(e.Graphics, e.ImageRectangle, radio.Enabled);
                return;
            }
            // ライトテーマは文字色（黒）だと強すぎるので、少し明るい色で描く
            Color color = e.Item is not { Enabled: true } item ? SystemColors.GrayText
                : ColorTable is ThemedProfessionalColorTable ? item.ForeColor
                : AccentColorTable.LightCheckGlyph;
            Image? glyph = MenuCheckGlyph.Get(e.ImageRectangle.Size, color);
            if (glyph == null)
            {
                base.OnRenderItemCheck(e); // 画像が読めない場合は標準のチェック
                return;
            }
            // 背景と枠は標準の処理で描き、画像だけ差し替える
            base.OnRenderItemCheck(new ToolStripItemImageRenderEventArgs(e.Graphics, e.Item, glyph, e.ImageRectangle));
        }
    }

    /// <summary>
    /// メニューバーで「開いているメニューから隣の項目へ移っている途中」かどうかを追跡する。
    /// ・隣へ移るとき: 前のドロップダウンが閉じ（理由 AppFocusChange）、前の項目の選択が外れ、別の項目が選択されている
    /// ・Esc で閉じたとき: 理由 Keyboard で、閉じた項目は選択されたまま（キーボード操作の強調は残す必要がある）
    /// 次のドロップダウンが開いた時点、またはメニュー操作が終わった時点で解除する。
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class MenuBarSwitchTracker
    {
        private sealed class State { public bool Switching; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MenuStrip, State> States = new();

        public static bool IsSwitching(MenuStrip menuStrip)
            => States.TryGetValue(menuStrip, out State? state) && state.Switching;

        /// <summary>MenuStrip とその直下の項目にイベントを付ける。テーマの再適用で何度呼ばれても 1 回だけ付ける。</summary>
        public static void Attach(MenuStrip menuStrip)
        {
            if (States.TryGetValue(menuStrip, out _)) return;
            var state = new State();
            States.Add(menuStrip, state);

            menuStrip.MenuDeactivate += (s, e) => state.Switching = false;
            foreach (ToolStripMenuItem item in menuStrip.Items.OfType<ToolStripMenuItem>())
            {
                item.DropDownOpened += (s, e) => state.Switching = false;
                item.DropDownClosed += (s, e) =>
                {
                    state.Switching = !item.Selected
                        && menuStrip.Items.OfType<ToolStripMenuItem>().Any(other => other != item && other.Selected && other.HasDropDown);
                };
            }
        }
    }

    /// <summary>
    /// 埋め込みのチェック画像（透過 PNG、形はアルファで表現）を指定サイズ・指定色で用意してキャッシュする。
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class MenuCheckGlyph
    {
        private const string RESOURCE_NAME = "GkmStatus.Resources.menu_check.png";
        private static readonly Lazy<Bitmap?> Source = new(LoadSource);
        private static readonly Dictionary<(Size, int), Bitmap> Cache = [];

        public static Image? Get(Size size, Color color)
        {
            if (size.Width <= 0 || size.Height <= 0 || Source.Value is not Bitmap src) return null;
            var key = (size, color.ToArgb());
            if (Cache.TryGetValue(key, out Bitmap? cached)) return cached;

            var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                // RGB を指定色に置き換え、アルファはそのまま使う
                using var attrs = new ImageAttributes();
                attrs.SetColorMatrix(new ColorMatrix(
                [
                    [0, 0, 0, 0, 0],
                    [0, 0, 0, 0, 0],
                    [0, 0, 0, 0, 0],
                    [0, 0, 0, color.A / 255f, 0],
                    [color.R / 255f, color.G / 255f, color.B / 255f, 0, 1],
                ]));
                g.DrawImage(src, new Rectangle(Point.Empty, size), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
            }
            Cache[key] = bmp;
            return bmp;
        }

        private static Bitmap? LoadSource()
        {
            try
            {
                using Stream? stream = typeof(MenuCheckGlyph).Assembly.GetManifestResourceStream(RESOURCE_NAME);
                return stream == null ? null : new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 択一（ラジオボタン式）のメニュー項目。選択中（Checked）の項目にはチェックの代わりにラジオボタンを描く。
    /// 選択の切り替え自体は通常の項目と同じく Checked で行う。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class RadioMenuItem(string text) : ToolStripMenuItem(text);

    /// <summary>
    /// 埋め込みのラジオボタン画像（選択中の円、色付きの透過 PNG）を指定サイズで用意してキャッシュし、描く。
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class MenuRadioGlyph
    {
        private const string RESOURCE_NAME = "GkmStatus.Resources.menu_radio.png";
        private static readonly Lazy<Bitmap?> Source = new(LoadSource);
        private static readonly Dictionary<int, Bitmap> Cache = [];

        /// <summary>bounds の中央に、短辺に合わせた正方形で描く。無効な項目は薄く描く。</summary>
        public static void Draw(Graphics g, Rectangle bounds, bool enabled)
        {
            int size = Math.Min(bounds.Width, bounds.Height);
            if (size <= 0 || Get(size) is not Bitmap glyph) return;
            var rect = new Rectangle(bounds.Left + (bounds.Width - size) / 2, bounds.Top + (bounds.Height - size) / 2, size, size);
            if (enabled)
            {
                g.DrawImage(glyph, rect);
                return;
            }
            using var attrs = new ImageAttributes();
            attrs.SetColorMatrix(new ColorMatrix { Matrix33 = 0.4f });
            g.DrawImage(glyph, rect, 0, 0, size, size, GraphicsUnit.Pixel, attrs);
        }

        private static Bitmap? Get(int size)
        {
            if (Source.Value is not Bitmap src) return null;
            if (Cache.TryGetValue(size, out Bitmap? cached)) return cached;

            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, size, size), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel);
            }
            Cache[size] = bmp;
            return bmp;
        }

        private static Bitmap? LoadSource()
        {
            try
            {
                using Stream? stream = typeof(MenuRadioGlyph).Assembly.GetManifestResourceStream(RESOURCE_NAME);
                return stream == null ? null : new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// チェック欄とホバー表示をアクセント色（#ff7609 基調）にした配色。ライトテーマ用で、それ以外は標準のまま。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class AccentColorTable : ProfessionalColorTable
    {
        public static readonly Color Accent = ColorTranslator.FromHtml("#ff7609");

        /// <summary>
        /// ライトテーマのホバー行の塗り（淡いオレンジ）。背景（ほぼ白）より赤が強い色は半透明の重ね塗りでは作れないため、不透明の色で塗る。
        /// </summary>
        public static readonly Color LightHover = ColorTranslator.FromHtml("#ffedc4");
        /// <summary>
        /// ライトテーマでホバー中の行にあるチェック欄の色。ホバー行の塗りを変える前の見た目
        /// （背景にアクセント色を透明度 56 と 96 で重ねた色）をそのまま保つ。
        /// </summary>
        private static readonly Color LightCheckHover = ColorTranslator.FromHtml("#fcb57d");
        /// <summary>ライトテーマのチェックマークの色（文字色の黒より少し明るくする）。</summary>
        public static readonly Color LightCheckGlyph = ColorTranslator.FromHtml("#323232");

        /// <summary>チェック欄（ホバーしていない行）の塗りの透明度。</summary>
        protected virtual int FillAlpha => 56;
        /// <summary>ホバー行の塗り。</summary>
        protected virtual Color HoverFill => LightHover;
        /// <summary>ホバー中の行にあるチェック欄の塗り（ホバー行の塗りの上に重なる）。</summary>
        protected virtual Color CheckHoverFill => LightCheckHover;

        // チェック欄
        public override Color CheckBackground => Color.FromArgb(FillAlpha, Accent);
        public override Color CheckSelectedBackground => CheckHoverFill;
        public override Color CheckPressedBackground => CheckHoverFill;
        public override Color ButtonSelectedBorder => Accent;

        // ホバー（ドロップダウンの項目 / メニューバーの項目）
        public override Color MenuItemSelected => HoverFill;
        public override Color MenuItemSelectedGradientBegin => HoverFill;
        public override Color MenuItemSelectedGradientEnd => HoverFill;
        public override Color MenuItemBorder => Accent;
    }

    /// <summary>
    /// ダーク/OLED テーマ用の配色。チェック欄とホバーはアクセント色、それ以外は暗い色に差し替える。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class ThemedProfessionalColorTable : AccentColorTable
    {
        public static readonly Color DarkText = Color.FromArgb(240, 240, 240);

        private readonly Color _bg;
        private readonly Color _border;
        private readonly Color _separator;

        public ThemedProfessionalColorTable(Color menuBg)
        {
            UseSystemColors = false;
            bool oled = menuBg.R == 0 && menuBg.G == 0 && menuBg.B == 0;
            _bg = menuBg;
            _border = oled ? Color.FromArgb(70, 70, 76) : Color.FromArgb(85, 88, 95);
            _separator = oled ? Color.FromArgb(55, 55, 60) : Color.FromArgb(75, 78, 84);
        }

        // 暗い背景では同じ透明度だと沈むので少し濃くする
        protected override int FillAlpha => 90;
        // ダーク/OLED はこれまでどおり、アクセント色の半透明の重ね塗り
        protected override Color HoverFill => Color.FromArgb(FillAlpha, Accent);
        protected override Color CheckHoverFill => Color.FromArgb(130, Accent);

        // メニューバー
        public override Color MenuStripGradientBegin => _bg;
        public override Color MenuStripGradientEnd => _bg;

        // ドロップダウン
        public override Color ToolStripDropDownBackground => _bg;
        public override Color ImageMarginGradientBegin => _bg;
        public override Color ImageMarginGradientMiddle => _bg;
        public override Color ImageMarginGradientEnd => _bg;
        public override Color MenuBorder => _border;

        // 押下中（ドロップダウンを開いている）のメニューバー項目はドロップダウンと同じ色にして一体に見せる
        public override Color MenuItemPressedGradientBegin => _bg;
        public override Color MenuItemPressedGradientMiddle => _bg;
        public override Color MenuItemPressedGradientEnd => _bg;

        // 区切り線
        public override Color SeparatorDark => _separator;
        public override Color SeparatorLight => _bg;
    }
}
