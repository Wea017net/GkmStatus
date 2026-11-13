using GkmStatus.src.ui;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using static GkmStatus.src.AppConstants;

namespace GkmStatus.src
{
    public enum AppTheme
    {
        Auto,
        Light,
        Dark,
        OLED
    }

    public partial class ThemeManager(FontManager fontManager)
    {
        [LibraryImport("dwmapi.dll")]
        private static partial int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_COLOR_DEFAULT = unchecked((int)0xFFFFFFFF);

        [LibraryImport("uxtheme.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int SetWindowTheme(nint hwnd, string? subAppName, string? subIdList);

        [StructLayout(LayoutKind.Sequential)]
        private struct COMBOBOXINFO
        {
            public int cbSize;
            // rcItem / rcButton（RECT 2 つ分）。中身は使わない
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

        private readonly FontManager _fontManager = fontManager;

        // システム標準のコントロールをダーク/OLED テーマで使うときの入力欄の背景色
        private static readonly Color DARK_INPUT_BG = Color.FromArgb(25, 25, 25);
        // DarkMode_CFD テーマで描かれる、閉じた状態のコンボボックスの面の色
        private static readonly Color DARK_COMBO_FACE = Color.FromArgb(51, 51, 51);
        // ライトテーマで NumericUpDown が描く入力欄の枠の色（無効時のテキスト入力欄をこれにそろえる）
        private static readonly Color LIGHT_DISABLED_INPUT_BORDER = Color.FromArgb(171, 173, 179);

        public void ApplyTheme(Form form, AppTheme theme)
        {
            Color color = GetThemeColor(theme);
            ApplyColorsToForm(form, color);
        }

        public void ApplyThemeToContextMenu(ContextMenuStrip menu, AppTheme theme)
        {
            Color bg = GetThemeColor(theme);
            bool isBright = bg.GetBrightness() > 0.5;
            Color menuBg = isBright ? Color.FromArgb(235, 233, 233) : (bg.R == 0 ? Color.Black : Color.FromArgb(45, 47, 51));

            // メインウィンドウのメニューバーのドロップダウンと同じ描画にする
            ProfessionalColorTable colorTable = CreateMenuColorTable(menuBg, isBright);
            ApplyDropDownTheme(menu, colorTable, isBright ? null : menuBg);
            foreach (ToolStripItem item in menu.Items)
            {
                ApplyMenuItemTheme(item, colorTable, isBright ? null : menuBg);
            }
        }

        private Color GetThemeColor(AppTheme theme)
        {
            return theme switch
            {
                AppTheme.Auto => GetWindowsThemeColor(),
                AppTheme.Light => COLOR_LIGHT_BG,
                AppTheme.Dark => COLOR_DARK_BG,
                AppTheme.OLED => COLOR_OLED_BG,
                _ => COLOR_DARK_BG
            };
        }

        private static Color GetWindowsThemeColor()
        {
            try
            {
                var value = Registry.GetValue(WINDOWS_THEME_REG_KEY, "AppsUseLightTheme", null);
                if (value is int i && i == 1)
                    return COLOR_LIGHT_BG;
            }
            catch { }
            return COLOR_DARK_BG;
        }

        private void ApplyColorsToForm(Form form, Color bg)
        {
            form.SuspendLayout();

            bool isBright = bg.GetBrightness() > 0.5;
            form.BackColor = bg;
            form.ForeColor = isBright ? COLOR_LIGHT_TEXT : Color.White;

            SetTitleBarDarkMode(form.Handle, !isBright);

            Color menuBg = isBright ? Color.FromArgb(235, 233, 233) : (bg.R == 0 ? Color.Black : Color.FromArgb(45, 47, 51));
            foreach (Control c in GetAllControls(form))
            {
                ApplyToControl(c, isBright, menuBg);
            }

            form.ResumeLayout(true);
        }

        private void ApplyToControl(Control c, bool isBright, Color menuBg)
        {
            if (c is Label lbl)
            {
                ApplyLabelTheme(lbl, isBright);
            }
            else if (c is MenuStrip ms)
            {
                ApplyMenuStripTheme(ms, menuBg, isBright);
            }
            else if (c is AccentComboBox cb)
            {
                ApplyComboBoxTheme(cb, isBright);
            }
            else if (c is TextBox tb && tb.Parent is not NumericUpDown) // NumericUpDown 内部の入力部分は下で扱う
            {
                // システム標準の描画。ダーク/OLED テーマでは Windows 標準のダーク表示（DarkMode_CFD）にする。
                tb.BackColor = isBright ? SystemColors.Window : DARK_INPUT_BG;
                tb.ForeColor = isBright ? COLOR_LIGHT_TEXT : Color.White;
                try { SetWindowTheme(tb.Handle, isBright ? null : "DarkMode_CFD", null); } catch { }
                // IME の未確定文字は既定では IME 側の白いウィンドウに表示されるため、ダーク時は入力欄の色で自前表示する
                if (tb is ImeCompositionTextBox imeTextBox)
                {
                    imeTextBox.DrawCompositionInline = !isBright;
                    // ライトテーマの無効時は枠がほぼ見えなくなるので、数字入力欄（NumericUpDown）の無効時と同じ枠にする
                    imeTextBox.DisabledBorderColor = isBright ? LIGHT_DISABLED_INPUT_BORDER : null;
                    imeTextBox.FocusBorderColor = AccentColorTable.Accent; // 入力中の枠はアクセント色（全テーマ共通）
                }
            }
            else if (c is NumericUpDown nu)
            {
                // システム標準の描画。色はフォームを継承してしまうので、ライトはシステム色、ダーク/OLED は暗い色を明示する。
                nu.BackColor = isBright ? SystemColors.Window : DARK_INPUT_BG;
                nu.ForeColor = isBright ? COLOR_LIGHT_TEXT : Color.White;
                try
                {
                    SetWindowTheme(nu.Handle, isBright ? null : "DarkMode_Explorer", null);
                    foreach (Control child in nu.Controls) // 上下ボタンと入力部分
                        SetWindowTheme(child.Handle, isBright ? null : "DarkMode_Explorer", null);
                    nu.Invalidate(true);
                }
                catch { }
            }
            else if (c is CheckBox chk)
            {
                chk.ForeColor = isBright ? COLOR_LIGHT_TEXT : Color.White;
            }
            else if (c is Button btn)
            {
                btn.Font = _fontManager.AppFontBold;
            }

            if (c is not Label && c is not MenuStrip && c is not Button)
            {
                c.Font = _fontManager.AppFontMedium;
            }
        }

        // コンボボックスはシステム標準の描画を使う。ダーク/OLED テーマでは Windows 標準のダーク表示
        // （エクスプローラーの「ファイルを開く」ダイアログと同じ DarkMode_CFD テーマ）に切り替える。
        private static void ApplyComboBoxTheme(AccentComboBox cb, bool isBright)
        {
            // 色は指定しないとフォームの色を継承するので明示する。
            // BackColor は閉じた状態の表示部分の背景になるため、ダーク時は DarkMode_CFD のボタン面と同じ色にし、
            // 開いた一覧の背景は ListBackColor で別に指定する。
            cb.BackColor = isBright ? SystemColors.Window : DARK_COMBO_FACE;
            cb.ListBackColor = isBright ? SystemColors.Window : DARK_INPUT_BG;
            cb.ForeColor = isBright ? COLOR_LIGHT_TEXT : Color.White;
            try
            {
                SetWindowTheme(cb.Handle, isBright ? null : "DarkMode_CFD", null);
                var info = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
                if (GetComboBoxInfo(cb.Handle, ref info) && info.hwndList != 0)
                    SetWindowTheme(info.hwndList, isBright ? null : "DarkMode_Explorer", null); // 一覧のスクロールバー
            }
            catch { }
        }

        private void ApplyLabelTheme(Label lbl, bool isBright)
        {
            if (lbl.Name == FOOTER_SEPARATOR_NAME)
            {
                // ライトは立体の線、ダーク/OLED は背景になじむ色の平らな線
                bool oled = lbl.FindForm()?.BackColor.R == 0;
                lbl.BorderStyle = isBright ? BorderStyle.Fixed3D : BorderStyle.None;
                if (isBright) lbl.ResetBackColor();
                else lbl.BackColor = oled ? COLOR_OLED_SEPARATOR : COLOR_DARK_SEPARATOR;
                return;
            }

            if (lbl.Tag is string tag && tag.StartsWith("Header_"))
                lbl.Font = _fontManager.AppFontBold;
            else
                lbl.Font = _fontManager.AppFontMedium;

            if (lbl.Name == "lblResetGuide" || lbl.Name == "lblBtnModeNote")
                lbl.ForeColor = Color.Gray;
            else if (lbl.Name == "lblGameAppGuide")
                lbl.ForeColor = AppConstants.COLOR_PAUSE;
            else if (lbl.Name == "lblStatus")
            {
                // do not overwrite color of lblStatus here as it is dynamic
            }
            else
                lbl.ForeColor = isBright ? COLOR_LIGHT_TEXT : Color.LightGray;
        }

        // メインウィンドウのメニューバー: 標準の ToolStripProfessionalRenderer をベースに、テーマに合わせた ColorTable を渡す。
        // フォントはシステムのメニューフォント（ToolStripManager.DefaultFont）。
        // 全テーマ共通で、チェック欄とホバーはアクセント色（#ff7609）、チェックマークは独自画像、矢印は文字色。
        private static void ApplyMenuStripTheme(MenuStrip ms, Color menuBg, bool isBright)
        {
            // ライトテーマは背景・タイトルバーと区別できる明るい色、ダーク/OLED テーマはテーマの暗い色。
            // MenuStrip の BackColor/ForeColor はフォームの値を継承してしまうため、ライトでも明示する。
            ms.BackColor = isBright ? COLOR_LIGHT_MENUBAR : menuBg;
            ms.ForeColor = isBright ? SystemColors.MenuText : ThemedProfessionalColorTable.DarkText;
            ms.ResetFont();

            ProfessionalColorTable colorTable = CreateMenuColorTable(menuBg, isBright);
            ms.Renderer = new ThemedProfessionalRenderer(colorTable);
            MenuBarSwitchTracker.Attach(ms);
            foreach (ToolStripItem item in ms.Items)
            {
                ApplyMenuItemTheme(item, colorTable, isBright ? null : menuBg);
            }
        }

        // ライトテーマは標準の配色にアクセント色を加えたもの、ダーク/OLED テーマは暗い配色
        private static ProfessionalColorTable CreateMenuColorTable(Color menuBg, bool isBright)
            => isBright ? new AccentColorTable() : new ThemedProfessionalColorTable(menuBg);

        private static void ApplyMenuItemTheme(ToolStripItem item, ProfessionalColorTable colorTable, Color? darkBg)
        {
            item.ResetBackColor();
            item.ResetForeColor();
            item.ResetFont();
            if (item is not ToolStripMenuItem menuItem) return;

            if (menuItem.HasDropDown) ApplyDropDownTheme(menuItem.DropDown, colorTable, darkBg);
            foreach (ToolStripItem subItem in menuItem.DropDownItems)
            {
                ApplyMenuItemTheme(subItem, colorTable, darkBg);
            }
        }

        // ドロップダウン（メニューバーのサブメニュー / トレイのコンテキストメニュー）。darkBg が null ならシステム色。
        private static void ApplyDropDownTheme(ToolStripDropDown dropDown, ProfessionalColorTable colorTable, Color? darkBg)
        {
            if (darkBg is Color bg)
            {
                dropDown.BackColor = bg;
                dropDown.ForeColor = ThemedProfessionalColorTable.DarkText;
            }
            else
            {
                dropDown.ResetBackColor();
                dropDown.ResetForeColor();
            }
            dropDown.ResetFont();
            dropDown.Renderer = new ThemedProfessionalRenderer(colorTable);

            // チェックやラジオが付く項目（とアイコン）が 1 つもないメニューは、左端のチェック欄を詰める。
            // 項目はあとから追加されることがある（トレイのプロデュースアイドル一覧など）ので、開くたびに決める
            dropDown.Opening -= UpdateCheckMargin;
            dropDown.Opening += UpdateCheckMargin;
        }

        private static void UpdateCheckMargin(object? sender, EventArgs e)
        {
            if (sender is not ToolStripDropDownMenu menu) return;
            // 欄を詰めたメニューの文字の位置は ThemedProfessionalRenderer.OnRenderItemText で少し右へずらす
            menu.ShowImageMargin = menu.Items.OfType<ToolStripMenuItem>()
                .Any(item => item.CheckOnClick || item.Checked || item is RadioMenuItem || item.Image != null);
        }


        private static void SetTitleBarDarkMode(IntPtr handle, bool dark)
        {
            try
            {
                int useDark = dark ? 1 : 0;
                if (DwmSetWindowAttribute(handle, 20, ref useDark, sizeof(int)) != 0)
                    _ = DwmSetWindowAttribute(handle, 19, ref useDark, sizeof(int));

                // ライトテーマはタイトルバーを白にしてメニューバーと区別する（Windows 11 以降のみ有効）。
                // ダーク/OLED はシステムの既定に戻す
                Color title = COLOR_LIGHT_TITLEBAR;
                int caption = dark ? DWMWA_COLOR_DEFAULT : title.R | (title.G << 8) | (title.B << 16); // COLORREF（0x00BBGGRR）
                _ = DwmSetWindowAttribute(handle, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            }
            catch { }
        }

        private static IEnumerable<Control> GetAllControls(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                yield return c;
                foreach (var sub in GetAllControls(c)) yield return sub;
            }
        }

        public static void UpdateThemeChecks(ToolStripMenuItem selected, params ToolStripMenuItem[] others)
        {
            selected.Checked = true;
            foreach (var o in others)
                o.Checked = false;
        }

    }
}
