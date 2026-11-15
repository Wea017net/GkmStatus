using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using GkmStatus.src;
using GkmStatus.src.ui;

namespace GkmStatus
{
    /// <summary>
    /// 「このアプリについて」のダイアログ。配色・フォント・下部のボタンはメイン画面（MainForm）と同じにする。
    /// </summary>
    public partial class AboutForm : Form
    {
        private readonly FontManager _fontManager;
        private readonly ThemeManager _themeManager;
        private readonly AppTheme _theme;
        private readonly float _scale;
        private readonly Font _titleFont;
        private Image? _iconImage;
        private Icon? _ownedIcon;

        public AboutForm(FontManager fontManager, ThemeManager themeManager, AppTheme theme, float scale)
        {
            _fontManager = fontManager;
            _themeManager = themeManager;
            _theme = theme;
            _scale = scale;
            // アプリ名はメイン画面の見出しと同じ太字を大きくしたもの
            Font bold = fontManager.AppFontBold;
            _titleFont = new Font(bold.FontFamily, bold.Size * 1.4f, bold.Style, GraphicsUnit.Pixel);

            this.Disposed += (s, e) =>
            {
                _iconImage?.Dispose();
                _ownedIcon?.Dispose();
                _titleFont.Dispose();
            };

            SuspendLayout();
            InitializeComponent();
            ApplyComponent();
            ResumeLayout();
        }

        /// <summary>
        /// メイン画面と同じテーマを当て、そのあとでこのダイアログだけの文字の大きさと色を付け直す
        /// （ThemeManager はラベルのフォントと色を一律に設定するため）。
        /// </summary>
        private void ApplyTheme()
        {
            _themeManager.ApplyTheme(this, _theme);
            lblName.Font = _titleFont;
            foreach (var note in new[] { lblVersion, lblFont })
                note.ForeColor = Color.Gray; // メイン画面の補足（「Discordの仕様により…」など）と同じ
        }

        /// <summary>同梱フォント（IBM Plex Sans JP）のライセンスを、メイン画面と同じテーマのウィンドウで表示する。</summary>
        private void ShowLicense()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using Stream? stream = assembly.GetManifestResourceStream("GkmStatus.Resources.fonts.IBM_Plex_Sans_JP.OFL.txt");
                if (stream == null) return;

                using var reader = new StreamReader(stream);
                string licenseText = reader.ReadToEnd();
                using var licenseFont = new Font(FontFamily.GenericMonospace, 9f * _scale * 96f / 72f, GraphicsUnit.Pixel);
                using var licenseForm = new Form
                {
                    Text = "Font License",
                    Icon = this.Icon,
                    AutoScaleMode = AutoScaleMode.None,
                    ClientSize = new Size(S(500), S(400)),
                    StartPosition = FormStartPosition.CenterParent,
                    ShowInTaskbar = false,
                    MinimizeBox = false,
                    MaximizeBox = false
                };
                var txt = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    Dock = DockStyle.Fill,
                    Text = licenseText,
                    ScrollBars = ScrollBars.Vertical,
                    BorderStyle = BorderStyle.None
                };
                licenseForm.Controls.Add(txt);
                // 入力欄の色・ダーク表示・タイトルバーはメイン画面と同じにし、文字だけ等幅にする
                _themeManager.ApplyTheme(licenseForm, _theme);
                txt.Font = licenseFont;
                txt.Select(0, 0);
                licenseForm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                ErrorLog.Write("About license", ex);
                MessageBox.Show(this, "Could not load license: " + ex.Message, I18n.T(I18n.Text_List.App_Name), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetOwnedIconImage(Image image) => _iconImage = new Bitmap(image);

        private void SetOwnedIcon(Icon icon)
        {
            _ownedIcon?.Dispose();
            _ownedIcon = icon;
            this.Icon = icon;
        }
    }
}
