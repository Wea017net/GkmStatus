// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

namespace GkmStatus
{
    public partial class AboutForm : Form
    {
        private readonly Color _backColor;
        private readonly Color _foreColor;
        private readonly Font _mainFont;
        private readonly Font _titleFont;
        private readonly float _scale;

        public AboutForm(Color backColor, Color foreColor, Font mainFont, float scale)
        {
            _backColor = backColor;
            _foreColor = foreColor;
            _mainFont = mainFont;
            _scale = scale;
            _titleFont = new Font(mainFont.FontFamily, 14f, FontStyle.Bold, GraphicsUnit.Point);

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = I18n.T("About_Title");
            this.AutoScaleMode = AutoScaleMode.None;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = _backColor;
            this.ForeColor = _foreColor;
            this.ClientSize = new Size(S(320), S(280));

            // Icon
            var iconBox = new PictureBox
            {
                Size = new Size(S(64), S(64)),
                Location = new Point((this.ClientSize.Width - S(64)) / 2, S(25)),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using Stream? stream = assembly.GetManifestResourceStream("GkmStatus.Resources.app_icon_2048px.png");
                if (stream != null)
                {
                    iconBox.Image = Image.FromStream(stream);
                }
                using Stream? icoStream = assembly.GetManifestResourceStream("GkmStatus.Resources.app.ico");
                if (icoStream != null) this.Icon = new Icon(icoStream);
            }
            catch { }
            this.Controls.Add(iconBox);

            // App Name
            var lblName = new Label
            {
                Text = "GkmStatus",
                Font = _titleFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, S(95)),
                Size = new Size(this.ClientSize.Width, S(30))
            };
            this.Controls.Add(lblName);

            // Version
            var lblVersion = new Label
            {
                Text = "v" + Application.ProductVersion,
                Font = _mainFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, S(125)),
                Size = new Size(this.ClientSize.Width, S(20)),
                ForeColor = _foreColor.GetBrightness() > 0.5f ? Color.FromArgb(100, 100, 100) : Color.FromArgb(180, 180, 180)
            };
            this.Controls.Add(lblVersion);

            // Author
            var lblAuthor = new Label
            {
                Text = I18n.T("About_Author"),
                Font = _mainFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, S(150)),
                Size = new Size(this.ClientSize.Width, S(20))
            };
            this.Controls.Add(lblAuthor);

            // Font Attribution
            var lblFont = new Label
            {
                Text = I18n.T("About_FontAttribution"),
                Font = new Font(_mainFont.FontFamily, 8f, GraphicsUnit.Point),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, S(172)),
                Size = new Size(this.ClientSize.Width, S(15)),
                ForeColor = _foreColor.GetBrightness() > 0.5f ? Color.FromArgb(120, 120, 120) : Color.FromArgb(150, 150, 150)
            };
            this.Controls.Add(lblFont);

            var lnkLicense = new LinkLabel
            {
                Text = I18n.T("About_FontLicense"),
                Font = new Font(_mainFont.FontFamily, 8f, GraphicsUnit.Point),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, S(187)),
                Size = new Size(this.ClientSize.Width, S(15)),
                LinkColor = COLOR_PRIMARY_DEFAULT,
                ActiveLinkColor = Color.White,
                VisitedLinkColor = COLOR_PRIMARY_DEFAULT
            };
            lnkLicense.LinkClicked += (s, e) => ShowLicense();
            this.Controls.Add(lnkLicense);

            // OK Button
            var btnOk = new Button
            {
                Text = "OK",
                Size = new Size(S(90), S(35)),
                Location = new Point((this.ClientSize.Width - S(90)) / 2, S(225)),
                FlatStyle = FlatStyle.Flat,
                Font = _mainFont,
                Cursor = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderColor = _foreColor.GetBrightness() > 0.5f ? Color.Gray : Color.FromArgb(100, 100, 100);
            btnOk.Click += (s, e) => this.Close();
            this.Controls.Add(btnOk);
        }

        private static readonly Color COLOR_PRIMARY_DEFAULT = ColorTranslator.FromHtml("#7e87f4");

        private void ShowLicense()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using Stream? stream = assembly.GetManifestResourceStream("GkmStatus.Resources.fonts.IBM_Plex_Sans_JP.OFL.txt");
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    string licenseText = reader.ReadToEnd();
                    using var licenseForm = new Form
                    {
                        Text = "Font License",
                        Icon = this.Icon,
                        ShowIcon = true,
                        Size = new Size(S(500), S(400)),
                        StartPosition = FormStartPosition.CenterParent,
                        BackColor = _backColor,
                        ForeColor = _foreColor,
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
                        BackColor = _backColor,
                        ForeColor = _foreColor,
                        BorderStyle = BorderStyle.None,
                        Font = new Font(FontFamily.GenericMonospace, 9f, GraphicsUnit.Point)
                    };
                    licenseForm.Controls.Add(txt);
                    licenseForm.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load license: " + ex.Message);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SetTitleBarDarkMode(_backColor.GetBrightness() < 0.5f);
        }

        private int S(int val) => (int)Math.Round(val * _scale);

        [System.Runtime.InteropServices.LibraryImport("dwmapi.dll")]
        private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private void SetTitleBarDarkMode(bool dark)
        {
            try
            {
                int useDark = dark ? 1 : 0;
                int hr = DwmSetWindowAttribute(this.Handle, 20, ref useDark, sizeof(int));
                if (hr != 0) _ = DwmSetWindowAttribute(this.Handle, 19, ref useDark, sizeof(int));
            }
            catch { }
        }
    }
}
