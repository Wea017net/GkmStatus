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

        private readonly FontManager _fontManager = fontManager;

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
            form.ForeColor = isBright ? Color.Black : Color.White;

            SetTitleBarDarkMode(form.Handle, !isBright);

            Color menuBg = isBright ? Color.FromArgb(235, 233, 233) : (bg.R == 0 ? Color.Black : Color.FromArgb(45, 47, 51));
            Color controlBg = isBright ? Color.White : Color.FromArgb(47, 49, 54);
            Color borderColor = isBright ? Color.FromArgb(200, 200, 200) : Color.FromArgb(70, 70, 70);

            foreach (Control c in GetAllControls(form))
            {
                ApplyToControl(c, isBright, menuBg, controlBg, borderColor);
            }

            form.ResumeLayout(true);
        }

        private void ApplyToControl(Control c, bool isBright, Color menuBg, Color controlBg, Color borderColor)
        {
            if (c is Label lbl)
            {
                ApplyLabelTheme(lbl, isBright);
            }
            else if (c is MenuStrip ms)
            {
                ApplyMenuStripTheme(ms, menuBg, isBright);
            }
            else if (c is ComboBox cb)
            {
                cb.BackColor = controlBg;
                cb.ForeColor = isBright ? Color.Black : Color.White;
            }
            else if (c is TextBox tb)
            {
                tb.BackColor = controlBg;
                tb.ForeColor = isBright ? Color.Black : Color.White;
                if (tb.Tag is Panel p) { UpdatePanelBorder(p, controlBg, borderColor); }
            }
            else if (c is NumericUpDown nu)
            {
                nu.BackColor = controlBg;
                nu.ForeColor = isBright ? Color.Black : Color.White;
                if (nu.Tag is Panel p) { UpdatePanelBorder(p, controlBg, borderColor); }
            }
            else if (c is CheckBox chk)
            {
                chk.ForeColor = isBright ? Color.Black : Color.White;
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

        private void ApplyLabelTheme(Label lbl, bool isBright)
        {
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
                lbl.ForeColor = isBright ? Color.Black : Color.LightGray;
        }

        // メインウィンドウのメニューバー: 標準の ToolStripProfessionalRenderer をベースに、テーマに合わせた ColorTable を渡す。
        // フォントはシステムのメニューフォント（ToolStripManager.DefaultFont）。
        // 全テーマ共通で、チェック欄とホバーはアクセント色（#ff7609）、チェックマークは独自画像、矢印は文字色。
        private static void ApplyMenuStripTheme(MenuStrip ms, Color menuBg, bool isBright)
        {
            // ライトテーマはシステム色、ダーク/OLED テーマはテーマの暗い色。
            // MenuStrip の BackColor/ForeColor はフォームの値を継承してしまうため、ライトでも明示する。
            ms.BackColor = isBright ? SystemColors.MenuBar : menuBg;
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
        }

        private static void UpdatePanelBorder(Panel p, Color bg, Color border)
        {
            p.BackColor = bg;
            p.Tag = border;
            p.Invalidate();
        }

        private static void SetTitleBarDarkMode(IntPtr handle, bool dark)
        {
            try
            {
                int useDark = dark ? 1 : 0;
                if (DwmSetWindowAttribute(handle, 20, ref useDark, sizeof(int)) != 0)
                    _ = DwmSetWindowAttribute(handle, 19, ref useDark, sizeof(int));
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
