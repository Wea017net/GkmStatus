using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace GkmStatus.src.ui
{
    /// <summary>
    /// 標準の ToolStripProfessionalRenderer をベースに、次の 2 点だけを変える（メインウィンドウのメニューバー用）。
    /// ・サブメニュー矢印の色を項目の文字色に合わせる（既定ではダーク背景でも黒で描かれて見えないため）
    /// ・チェックマークの形を独自画像（Resources/menu_check.png）にし、項目の文字色で塗って描く
    /// チェック欄の背景・枠は標準どおり ColorTable の色で描く。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class ThemedProfessionalRenderer(ProfessionalColorTable colorTable) : ToolStripProfessionalRenderer(colorTable)
    {
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            if (e.Item is { Enabled: true } item) e.ArrowColor = item.ForeColor;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Color color = e.Item is { Enabled: true } item ? item.ForeColor : SystemColors.GrayText;
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
    /// チェック欄とホバー表示をアクセント色（#ff7609 基調）にした配色。ライトテーマ用で、それ以外は標準のまま。
    /// 塗りは半透明なので、下の色に自然に馴染む。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class AccentColorTable : ProfessionalColorTable
    {
        public static readonly Color Accent = ColorTranslator.FromHtml("#ff7609");

        /// <summary>チェック欄とホバー行の塗りの透明度。</summary>
        protected virtual int FillAlpha => 56;
        /// <summary>ホバー中の行にあるチェック欄の透明度（ホバー行の塗りの上に重なる）。</summary>
        protected virtual int CheckHoverAlpha => 96;

        // チェック欄
        public override Color CheckBackground => Color.FromArgb(FillAlpha, Accent);
        public override Color CheckSelectedBackground => Color.FromArgb(CheckHoverAlpha, Accent);
        public override Color CheckPressedBackground => Color.FromArgb(CheckHoverAlpha, Accent);
        public override Color ButtonSelectedBorder => Accent;

        // ホバー（ドロップダウンの項目 / メニューバーの項目）
        public override Color MenuItemSelected => Color.FromArgb(FillAlpha, Accent);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(FillAlpha, Accent);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(FillAlpha, Accent);
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
        protected override int CheckHoverAlpha => 130;

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
