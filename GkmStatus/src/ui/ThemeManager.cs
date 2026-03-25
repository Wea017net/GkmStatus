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
            Color color = theme switch
            {
                AppTheme.Auto => GetWindowsThemeColor(),
                AppTheme.Light => COLOR_LIGHT_BG,
                AppTheme.Dark => COLOR_DARK_BG,
                AppTheme.OLED => COLOR_OLED_BG,
                _ => COLOR_DARK_BG
            };

            ApplyColorsToForm(form, color);
        }

        private static Color GetWindowsThemeColor()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(WINDOWS_THEME_REG_KEY);
                var value = key?.GetValue("AppsUseLightTheme");
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
                ms.BackColor = menuBg;
                ms.Font = _fontManager.MenuFont;
                ms.Renderer = new MyRenderer(isBright, new CustomColorTable(isBright));
                foreach (ToolStripItem item in ms.Items)
                {
                    if (item is ToolStripMenuItem tsmi) ApplyThemeToMenuItems(tsmi, menuBg, isBright);
                }
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

            if (c is not Label && c is not MenuStrip)
            {
                c.Font = _fontManager.AppFont;
            }
        }

        private void ApplyLabelTheme(Label lbl, bool isBright)
        {
            if (lbl.Tag is string tag && tag.StartsWith("Header_"))
                lbl.Font = _fontManager.AppFontBold;
            else
                lbl.Font = _fontManager.AppFont;

            if (lbl.Name == "lblResetGuide" || lbl.Name == "lblBtnModeNote")
                lbl.ForeColor = Color.Gray;
            else if (lbl.Name == "lblGameAppGuide")
                lbl.ForeColor = AppConstants.COLOR_PAUSE;
            else
                lbl.ForeColor = isBright ? Color.Black : Color.LightGray;
        }

        private void ApplyThemeToMenuItems(ToolStripMenuItem item, Color menuBg, bool isBright)
        {
            item.Font = _fontManager.MenuFont;
            if (item.DropDown != null)
            {
                item.DropDown.BackColor = menuBg;
                item.DropDown.Font = _fontManager.MenuFont;
                item.DropDown.Renderer = new MyRenderer(isBright, new CustomColorTable(isBright));
            }
            foreach (ToolStripItem subItem in item.DropDownItems)
            {
                if (subItem is ToolStripMenuItem subMenu) ApplyThemeToMenuItems(subMenu, menuBg, isBright);
                else subItem.Font = _fontManager.MenuFont;
            }
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
