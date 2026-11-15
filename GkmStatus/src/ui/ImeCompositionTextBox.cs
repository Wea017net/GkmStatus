using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GkmStatus.src.ui
{
    /// <summary>
    /// IME の未確定文字（変換中の文字）を、IME 側のウィンドウではなく入力欄の色で表示できる TextBox。
    /// 標準の TextBox（EDIT コントロール）は未確定文字を IME の入力ウィンドウに任せるが、そのウィンドウは
    /// システム色（白背景）で描かれ、色を変える API もない。ダークテーマでは白く浮くため、
    /// DrawCompositionInline が true のときは IME の入力ウィンドウを出さず、キャレット位置に重ねた
    /// 自前のオーバーレイ（ImeCompositionOverlay）に未確定文字を描く。確定した文字は通常どおり TextBox に入る。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class ImeCompositionTextBox : TextBox
    {
        private const int WM_IME_STARTCOMPOSITION = 0x010D;
        private const int WM_IME_ENDCOMPOSITION = 0x010E;
        private const int WM_IME_COMPOSITION = 0x010F;

        private const int GCS_COMPSTR = 0x0008;
        private const int GCS_COMPATTR = 0x0010;
        private const int GCS_CURSORPOS = 0x0080;
        private const int GCS_RESULT_MASK = 0x0200 | 0x0400 | 0x0800 | 0x1000; // RESULTREADSTR / RESULTREADCLAUSE / RESULTSTR / RESULTCLAUSE

        private const int CFS_POINT = 0x0002;
        private const int CFS_EXCLUDE = 0x0080;

        [StructLayout(LayoutKind.Sequential)]
        private struct COMPOSITIONFORM
        {
            public int dwStyle;
            public int ptX, ptY;
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CANDIDATEFORM
        {
            public int dwIndex;
            public int dwStyle;
            public int ptX, ptY;
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [LibraryImport("imm32.dll")]
        private static partial nint ImmGetContext(nint hWnd);
        [LibraryImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ImmReleaseContext(nint hWnd, nint hIMC);
        [LibraryImport("imm32.dll", EntryPoint = "ImmGetCompositionStringW")]
        private static partial int ImmGetCompositionString(nint hIMC, int dwIndex, byte[]? lpBuf, int dwBufLen);
        [LibraryImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ImmSetCompositionWindow(nint hIMC, ref COMPOSITIONFORM form);
        [LibraryImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ImmSetCandidateWindow(nint hIMC, ref CANDIDATEFORM form);
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetCaretPos(out POINT point);

        private ImeCompositionOverlay? _overlay;
        private Point _compositionOrigin; // 未確定文字を表示し始める位置（TextBox のクライアント座標）
        private bool _composing;

        private const int WM_NCPAINT = 0x0085;
        private const int RDW_INVALIDATE = 0x0001, RDW_FRAME = 0x0400;

        [LibraryImport("user32.dll")]
        private static partial nint GetWindowDC(nint hWnd);
        [LibraryImport("user32.dll")]
        private static partial int ReleaseDC(nint hWnd, nint hDC);
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool RedrawWindow(nint hWnd, nint lprcUpdate, nint hrgnUpdate, int flags);

        private Color? _disabledBorderColor;
        private Color? _focusBorderColor;

        /// <summary>
        /// 無効時に枠をこの色で描き直す（null なら Windows の描画のまま）。
        /// Windows 11 のテキスト入力欄は無効時の枠がほぼ背景と同じ色で見えなくなるため、ライトテーマでは
        /// NumericUpDown の無効時と同じ枠（外側の線 + 内側の白い線）にそろえる。
        /// </summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color? DisabledBorderColor
        {
            get => _disabledBorderColor;
            set { _disabledBorderColor = value; RedrawFrame(); }
        }

        /// <summary>入力中（フォーカスがある間）の枠の色（null なら Windows の描画のまま）。</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color? FocusBorderColor
        {
            get => _focusBorderColor;
            set { _focusBorderColor = value; RedrawFrame(); }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            RedrawFrame();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            RedrawFrame();
        }

        private void RedrawFrame()
        {
            if (IsHandleCreated) RedrawWindow(Handle, 0, 0, RDW_INVALIDATE | RDW_FRAME);
        }

        private void DrawFocusBorder(Color color)
        {
            nint hdc = GetWindowDC(Handle);
            if (hdc == 0) return;
            try
            {
                using Graphics g = Graphics.FromHdc(hdc);
                using var pen = new Pen(color);
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                // ライトの Windows 11 は入力中に枠の内側の下辺へ青い線を引くので、入力欄の色で消す（ダークの内側の線は灰色なのでそのまま）
                if (BackColor.GetBrightness() > 0.5f)
                {
                    using var inner = new Pen(BackColor);
                    g.DrawLine(inner, 1, Height - 2, Width - 2, Height - 2);
                }
            }
            finally
            {
                ReleaseDC(Handle, hdc);
            }
        }

        private void DrawDisabledBorder(Color color)
        {
            nint hdc = GetWindowDC(Handle);
            if (hdc == 0) return;
            try
            {
                using Graphics g = Graphics.FromHdc(hdc);
                using var outer = new Pen(color);
                g.DrawRectangle(outer, 0, 0, Width - 1, Height - 1);
                g.DrawRectangle(Pens.White, 1, 1, Width - 3, Height - 3);
            }
            finally
            {
                ReleaseDC(Handle, hdc);
            }
        }

        /// <summary>true のとき、未確定文字を入力欄の色（BackColor/ForeColor）で自前表示する。</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool DrawCompositionInline { get; set; }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCPAINT)
            {
                base.WndProc(ref m);
                if (BorderStyle != BorderStyle.None)
                {
                    if (!Enabled && _disabledBorderColor is Color border) DrawDisabledBorder(border);
                    else if (Enabled && Focused && _focusBorderColor is Color focus) DrawFocusBorder(focus);
                }
                return;
            }
            if (DrawCompositionInline)
            {
                switch (m.Msg)
                {
                    case WM_IME_STARTCOMPOSITION:
                        BeginComposition();
                        return; // 既定処理（IME の入力ウィンドウ表示）はしない

                    case WM_IME_COMPOSITION:
                        {
                            int flags = (int)m.LParam;
                            if ((flags & GCS_RESULT_MASK) != 0)
                            {
                                // 確定文字の挿入だけを既定処理に任せる（未確定文字の表示フラグは渡さない）
                                m.LParam = flags & GCS_RESULT_MASK;
                                base.WndProc(ref m);
                            }
                            if (!_composing) BeginComposition(); // 確定直後に続けて入力した場合など
                            UpdateComposition();
                            return;
                        }

                    case WM_IME_ENDCOMPOSITION:
                        EndComposition();
                        base.WndProc(ref m);
                        return;
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            EndComposition();
            base.OnLostFocus(e);
            RedrawFrame();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _overlay?.Dispose(); _overlay = null; }
            base.Dispose(disposing);
        }

        private void BeginComposition()
        {
            _composing = true;
            _compositionOrigin = GetCaretPos(out POINT caret) ? new Point(caret.X, caret.Y) : new Point(2, 2);
        }

        private void EndComposition()
        {
            _composing = false;
            if (_overlay != null) _overlay.Visible = false;
        }

        private void UpdateComposition()
        {
            nint himc = ImmGetContext(Handle);
            if (himc == 0) return;
            try
            {
                string text = ReadString(himc, GCS_COMPSTR);
                if (text.Length == 0)
                {
                    if (_overlay != null) _overlay.Visible = false;
                    return;
                }
                byte[] attrs = ReadBytes(himc, GCS_COMPATTR);
                int cursor = ImmGetCompositionString(himc, GCS_CURSORPOS, null, 0);

                ImeCompositionOverlay overlay = EnsureOverlay();
                if (overlay == null) return;
                overlay.SetComposition(text, attrs, cursor, Font, BackColor, ForeColor);

                // 表示位置: キャレット位置から。フォームの右端からはみ出す場合は左へずらす
                Point origin = Parent!.PointToClient(PointToScreen(_compositionOrigin));
                int maxRight = Parent.ClientSize.Width - 2;
                if (origin.X + overlay.Width > maxRight) origin.X = Math.Max(0, maxRight - overlay.Width);
                overlay.Location = origin;
                overlay.Visible = true;
                overlay.BringToFront();

                // 変換候補ウィンドウが未確定文字に重ならないよう、表示範囲を IME に伝える（座標は TextBox のクライアント座標）
                Point local = PointToClient(Parent.PointToScreen(origin));
                var candidate = new CANDIDATEFORM
                {
                    dwIndex = 0,
                    dwStyle = CFS_EXCLUDE,
                    ptX = local.X, ptY = local.Y + overlay.Height,
                    left = local.X, top = local.Y, right = local.X + overlay.Width, bottom = local.Y + overlay.Height
                };
                ImmSetCandidateWindow(himc, ref candidate);
                var composition = new COMPOSITIONFORM { dwStyle = CFS_POINT, ptX = local.X, ptY = local.Y };
                ImmSetCompositionWindow(himc, ref composition);
            }
            finally
            {
                ImmReleaseContext(Handle, himc);
            }
        }

        private ImeCompositionOverlay EnsureOverlay()
        {
            if (_overlay == null || _overlay.IsDisposed || _overlay.Parent != Parent)
            {
                _overlay?.Dispose();
                _overlay = new ImeCompositionOverlay { Visible = false };
                Parent?.Controls.Add(_overlay);
            }
            return _overlay;
        }

        private static byte[] ReadBytes(nint himc, int index)
        {
            int size = ImmGetCompositionString(himc, index, null, 0);
            if (size <= 0) return [];
            var buffer = new byte[size];
            ImmGetCompositionString(himc, index, buffer, size);
            return buffer;
        }

        private static string ReadString(nint himc, int index)
        {
            byte[] bytes = ReadBytes(himc, index);
            return bytes.Length == 0 ? "" : System.Text.Encoding.Unicode.GetString(bytes);
        }
    }

    /// <summary>
    /// 未確定文字を描くオーバーレイ。Windows の既定の見た目に合わせ、変換対象の文節は太い下線、それ以外は細い点線で示す。
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class ImeCompositionOverlay : Control
    {
        private const byte ATTR_TARGET_CONVERTED = 0x01;
        private const byte ATTR_TARGET_NOTCONVERTED = 0x03;

        private string _text = "";
        private byte[] _attrs = [];
        private int _cursor = -1;

        public ImeCompositionOverlay()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        public void SetComposition(string text, byte[] attrs, int cursor, Font font, Color back, Color fore)
        {
            _text = text;
            _attrs = attrs;
            _cursor = cursor;
            Font = font;
            BackColor = back;
            ForeColor = fore;
            Size textSize = TextRenderer.MeasureText(text, font, Size.Empty, Flags);
            Size = new Size(textSize.Width + 2, font.Height + 3);
            Invalidate();
        }

        private const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            TextRenderer.DrawText(g, _text, Font, new Point(0, 0), ForeColor, BackColor, Flags);

            int baseline = Font.Height;
            int x = 0;
            int i = 0;
            // 同じ属性が続く範囲（文節）ごとに下線を引く
            while (i < _text.Length)
            {
                byte attr = i < _attrs.Length ? _attrs[i] : (byte)0;
                int j = i + 1;
                while (j < _text.Length && (j < _attrs.Length ? _attrs[j] : (byte)0) == attr) j++;
                int width = TextRenderer.MeasureText(g, _text[i..j], Font, Size.Empty, Flags).Width;
                bool target = attr is ATTR_TARGET_CONVERTED or ATTR_TARGET_NOTCONVERTED;
                using (var pen = new Pen(ForeColor, target ? 2 : 1))
                {
                    if (!target) pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                    g.DrawLine(pen, x + 1, baseline, x + width - 2, baseline);
                }
                x += width;
                i = j;
            }

            // キャレット。変換中（変換対象の文節がある間）は Windows の既定どおり表示しない
            bool converting = Array.Exists(_attrs, a => a is ATTR_TARGET_CONVERTED or ATTR_TARGET_NOTCONVERTED);
            if (!converting && _cursor >= 0 && _cursor <= _text.Length)
            {
                int caretX = _cursor == 0 ? 0 : TextRenderer.MeasureText(g, _text[.._cursor], Font, Size.Empty, Flags).Width;
                using var caretPen = new Pen(ForeColor, 1);
                g.DrawLine(caretPen, caretX, 0, caretX, Font.Height - 1);
            }
        }
    }
}
