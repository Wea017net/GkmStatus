using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GkmStatus.src.ui
{
    /// <summary>
    /// 見た目はシステム標準のまま、次の 2 点だけをアクセント色（#ff7609）にした ComboBox（DropDownList 用）。
    /// ・選択中の項目のハイライト（一覧の選択行、フォーカス時の表示部分）: 項目を自前で描く（OwnerDrawFixed）
    /// ・開いた一覧の縁どり: 一覧ウィンドウの非クライアント描画のあとに枠を上描きする
    /// ハイライトはメニューと同じく、背景色にアクセント色を半透明で重ねた色。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class AccentComboBox : ComboBox
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct COMBOBOXINFO
        {
            public int cbSize;
            public int itemLeft, itemTop, itemRight, itemBottom;
            public int buttonLeft, buttonTop, buttonRight, buttonBottom;
            public int stateButton;
            public nint hwndCombo;
            public nint hwndItem;
            public nint hwndList;
        }

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetComboBoxInfo(nint hwndCombo, ref COMBOBOXINFO info);

        private ListBorderWindow? _listWindow;

        public AccentComboBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
        }

        /// <summary>アクセント色。</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color AccentColor { get; set; } = AccentColorTable.Accent;

        /// <summary>
        /// 開いた一覧の背景色。null なら BackColor。
        /// （BackColor は閉じた状態の表示部分の背景にも使われるため、一覧だけ別の色にしたいときに指定する）
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color? ListBackColor { get; set; }

        private Color ListBack => ListBackColor ?? BackColor;

        private bool IsDarkBackground => ListBack.GetBrightness() < 0.5f;

        /// <summary>
        /// 選択中の項目のハイライト色。メニューのホバーと同じで、ライトは淡いオレンジ（#ffedc4）、
        /// ダークは一覧の背景色にアクセント色を半透明で重ねた色。
        /// </summary>
        private Color HighlightColor => IsDarkBackground ? Blend(ListBack, AccentColor, 90) : AccentColorTable.LightHover;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateItemHeight();
            AttachListWindow();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateItemHeight();
        }

        private const int WM_PAINT = 0x000F;
        // uxtheme の COMBOBOX クラスのパーツと状態（vsstyle.h）
        private const int CP_BORDER = 4, CBB_NORMAL = 1;
        private const int CP_DROPDOWNBUTTONRIGHT = 6, CBXSR_NORMAL = 1;

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct PAINTSTRUCT
        {
            public nint hdc;
            public int fErase;
            public int left, top, right, bottom;
            public int fRestore;
            public int fIncUpdate;
            public fixed byte rgbReserved[32];
        }

        [LibraryImport("user32.dll")]
        private static partial nint BeginPaint(nint hWnd, out PAINTSTRUCT paint);
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool EndPaint(nint hWnd, in PAINTSTRUCT paint);

        protected override void WndProc(ref Message m)
        {
            // ライトテーマでは、Windows はホバー中と一覧を開いている間に外枠とボタンを青で描き、さらに状態の切り替えを
            // フェードのアニメーションで描く。Windows の描画の上から描き直すと一瞬元の絵が見えてちらつくため、
            // ライトテーマ（有効時）は WM_PAINT を自前で処理し、画面外で全体を描いてから一度に転送する。
            // ・通常 / 一覧を開いている間: 通常状態の外枠とボタン
            // ・ホバー中: 通常状態の見た目に、アクセント色の枠とボタンの薄い塗りを重ねる（メニューのホバーと同じ表現）
            // ダークテーマ（DarkMode_CFD）は青くならないので Windows の描画のまま。
            if (m.Msg == WM_PAINT && !IsDarkBackground && Enabled && System.Windows.Forms.VisualStyles.VisualStyleRenderer.IsSupported)
            {
                nint hdc = BeginPaint(Handle, out PAINTSTRUCT paint);
                try
                {
                    if (hdc != 0 && ClientSize.Width > 0 && ClientSize.Height > 0)
                    {
                        // 画面と同じ形式のバッファ（透過なし）に描くことで、ClearType やテーマ部品の半透明合成が画面と同じになる
                        using BufferedGraphics buffer = BufferedGraphicsManager.Current.Allocate(hdc, ClientRectangle);
                        PaintLight(buffer.Graphics);
                        buffer.Render();
                    }
                }
                finally
                {
                    EndPaint(Handle, in paint);
                }
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Invalidate();
        }

        // ライトテーマ時の全体の描画（WM_PAINT の代わり）
        private void PaintLight(Graphics g)
        {
            var info = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
            GetComboBoxInfo(Handle, ref info);
            Rectangle item = Rectangle.FromLTRB(info.itemLeft, info.itemTop, info.itemRight, info.itemBottom);
            Rectangle button = Rectangle.FromLTRB(info.buttonLeft, info.buttonTop, info.buttonRight, info.buttonBottom);
            bool hot = !DroppedDown && ClientRectangle.Contains(PointToClient(Cursor.Position));

            // 外枠（角の丸みの外側は親の背景色）
            using (var parentBack = new SolidBrush(Parent?.BackColor ?? SystemColors.Control))
                g.FillRectangle(parentBack, ClientRectangle);
            try
            {
                new System.Windows.Forms.VisualStyles.VisualStyleRenderer("COMBOBOX", CP_BORDER, CBB_NORMAL).DrawBackground(g, ClientRectangle);
                if (!button.IsEmpty)
                    new System.Windows.Forms.VisualStyles.VisualStyleRenderer("COMBOBOX", CP_DROPDOWNBUTTONRIGHT, CBXSR_NORMAL).DrawBackground(g, button);
            }
            catch (ArgumentException)
            {
                // テーマにパーツが定義されていない環境: 単純な枠で代用する
                g.FillRectangle(SystemBrushes.Window, ClientRectangle);
                ControlPaint.DrawBorder(g, ClientRectangle, SystemColors.ControlDark, ButtonBorderStyle.Solid);
                if (!button.IsEmpty) ControlPaint.DrawComboButton(g, button, ButtonState.Flat);
            }

            // 表示部分（選択中の項目）。背景はシステムと同じく BackColor で塗り、項目は OnDrawItem で描く
            if (!item.IsEmpty)
            {
                using (var back = new SolidBrush(BackColor)) g.FillRectangle(back, item);
                if (SelectedIndex >= 0)
                {
                    DrawItemState state = DrawItemState.ComboBoxEdit;
                    if (Focused && !DroppedDown) state |= DrawItemState.Selected | DrawItemState.Focus;
                    OnDrawItem(new DrawItemEventArgs(g, Font, item, SelectedIndex, state, ForeColor, BackColor));
                }
            }

            if (hot)
            {
                if (!button.IsEmpty) TintButton(g, button, AccentColorTable.LightHover);
                using var pen = new Pen(AccentColor);
                g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                g.DrawLine(pen, button.Left - 1, button.Top, button.Left - 1, button.Bottom - 1);
            }
        }

        protected override void OnDropDown(EventArgs e)
        {
            AttachListWindow(); // ハンドルが作り直された場合に備えて開くたびに確認する
            base.OnDropDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _listWindow?.ReleaseHandle(); _listWindow = null; }
            base.Dispose(disposing);
        }

        // 項目の高さは標準（DrawMode.Normal）と同じになるよう、フォントの高さ + 余白にする
        private void UpdateItemHeight()
        {
            int height = TextRenderer.MeasureText("Ag", Font).Height + 2;
            if (ItemHeight != height) ItemHeight = height;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            bool isEditArea = (e.State & DrawItemState.ComboBoxEdit) != 0; // 閉じた状態の表示部分
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color fore = Enabled ? ForeColor : SystemColors.GrayText;

            if (isEditArea)
            {
                // 表示部分の背景はシステムのテーマが描くので、フォーカス時のハイライトだけ重ねる
                if (selected && Focused && !DroppedDown)
                {
                    using var brush = new SolidBrush(IsDarkBackground ? Color.FromArgb(90, AccentColor) : AccentColorTable.LightHover);
                    e.Graphics.FillRectangle(brush, e.Bounds);
                }
            }
            else
            {
                using var brush = new SolidBrush(selected ? HighlightColor : ListBack);
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
            Rectangle textBounds = Rectangle.FromLTRB(e.Bounds.Left + 2, e.Bounds.Top, e.Bounds.Right - 2, e.Bounds.Bottom);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, textBounds, fore, flags);
        }

        /// <summary>
        /// 描き済みの矢印ボタンを指定色で「乗算」して描き直す。白い面は指定色そのもの（#ffedc4 など）になり、
        /// 矢印は暗いまま残る（不透明で塗りつぶすと矢印が消え、半透明で重ねると狙った色にならないため）。
        /// </summary>
        private static void TintButton(Graphics g, Rectangle button, Color tint)
        {
            using var copy = new Bitmap(button.Width, button.Height);
            using (Graphics cg = Graphics.FromImage(copy))
            {
                cg.Clear(Color.White);
                try
                {
                    new System.Windows.Forms.VisualStyles.VisualStyleRenderer("COMBOBOX", CP_DROPDOWNBUTTONRIGHT, CBXSR_NORMAL)
                        .DrawBackground(cg, new Rectangle(Point.Empty, button.Size));
                }
                catch (ArgumentException) { }
            }
            using var attrs = new System.Drawing.Imaging.ImageAttributes();
            attrs.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(
            [
                [tint.R / 255f, 0, 0, 0, 0],
                [0, tint.G / 255f, 0, 0, 0],
                [0, 0, tint.B / 255f, 0, 0],
                [0, 0, 0, 1, 0],
                [0, 0, 0, 0, 1],
            ]));
            g.DrawImage(copy, button, 0, 0, button.Width, button.Height, GraphicsUnit.Pixel, attrs);
        }

        private static Color Blend(Color background, Color overlay, int alpha)
        {
            int Mix(int b, int o) => (o * alpha + b * (255 - alpha)) / 255;
            return Color.FromArgb(Mix(background.R, overlay.R), Mix(background.G, overlay.G), Mix(background.B, overlay.B));
        }

        private void AttachListWindow()
        {
            if (!IsHandleCreated) return;
            var info = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
            if (!GetComboBoxInfo(Handle, ref info) || info.hwndList == 0) return;
            if (_listWindow?.Handle == info.hwndList) return;
            _listWindow?.ReleaseHandle();
            _listWindow = new ListBorderWindow(this);
            _listWindow.AssignHandle(info.hwndList);
        }

        /// <summary>開いた一覧のウィンドウ。標準の非クライアント描画（枠・スクロールバー）のあとに、枠をアクセント色で描き直す。</summary>
        private sealed partial class ListBorderWindow(AccentComboBox owner) : NativeWindow
        {
            private const int WM_NCPAINT = 0x0085;
            private const int WM_PRINT = 0x0317;

            [LibraryImport("user32.dll")]
            private static partial nint GetWindowDC(nint hWnd);
            [LibraryImport("user32.dll")]
            private static partial int ReleaseDC(nint hWnd, nint hDC);
            [LibraryImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static partial bool GetWindowRect(nint hWnd, out RECT rect);

            [StructLayout(LayoutKind.Sequential)]
            private struct RECT { public int Left, Top, Right, Bottom; }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg is WM_NCPAINT or WM_PRINT) DrawBorder();
            }

            private void DrawBorder()
            {
                if (!GetWindowRect(Handle, out RECT rect)) return;
                nint hdc = GetWindowDC(Handle);
                if (hdc == 0) return;
                try
                {
                    using Graphics g = Graphics.FromHdc(hdc);
                    using var pen = new Pen(owner.AccentColor);
                    g.DrawRectangle(pen, 0, 0, rect.Right - rect.Left - 1, rect.Bottom - rect.Top - 1);
                }
                finally
                {
                    ReleaseDC(Handle, hdc);
                }
            }
        }
    }
}
