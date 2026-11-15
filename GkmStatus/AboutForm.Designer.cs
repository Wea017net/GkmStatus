using GkmStatus.src;
using GkmStatus.src.ui;
using static GkmStatus.src.AppConstants;

namespace GkmStatus
{
    partial class AboutForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        // GUIDesignerをだます
        private void InitializeComponent()
        {

        }

        private int S(int val) => (int)Math.Round(val * _scale);

        private Label lblName;
        private Label lblVersion;
        private Label lblFont;

        // 動的に上書きする
        // 上から アイコン / アプリ名 / バージョン / 制作者 / 使用フォント / ライセンスのリンク、
        // 下部はメイン画面と同じく 区切り線 と ボタン（OK）。高さは文字の大きさから決める
        private void ApplyComponent()
        {
            this.Text = I18n.T(I18n.Text_List.App_Name);
            this.AutoScaleMode = AutoScaleMode.None;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            int width = S(320);

            int y = S(24);
            var iconBox = new PictureBox
            {
                Size = new Size(S(64), S(64)),
                Location = new Point((width - S(64)) / 2, y),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using Stream stream = assembly.GetManifestResourceStream("GkmStatus.Resources.app_icon_2048px.png");
                if (stream != null)
                {
                    using var sourceImage = Image.FromStream(stream);
                    SetOwnedIconImage(sourceImage);
                    iconBox.Image = _iconImage!;
                }
                using Stream icoStream = assembly.GetManifestResourceStream("GkmStatus.Resources.app.ico");
                if (icoStream != null) SetOwnedIcon(new Icon(icoStream));
            }
            catch { }
            this.Controls.Add(iconBox);
            y = iconBox.Bottom + S(10);

            // 中央そろえの 1 行。高さはフォントに合わせる
            Label AddLine(string text, Font font, int gapAfter)
            {
                int height = TextRenderer.MeasureText("Ag", font).Height + S(4);
                var label = new Label
                {
                    Text = text,
                    Font = font,
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Location = new Point(0, y),
                    Size = new Size(width, height)
                };
                this.Controls.Add(label);
                y += height + gapAfter;
                return label;
            }

            lblName = AddLine(I18n.T(I18n.Text_List.App_Name), _titleFont, 0);
            lblVersion = AddLine("v" + Application.ProductVersion, _fontManager.AppFontMedium, S(8));
            AddLine(I18n.T(I18n.Text_List.About_Author), _fontManager.AppFontMedium, S(8));
            lblFont = AddLine(I18n.T(I18n.Text_List.About_FontAttribution), _fontManager.AppFontMedium, 0);

            int linkHeight = TextRenderer.MeasureText("Ag", _fontManager.AppFontMedium).Height + S(4);
            var lnkLicense = new LinkLabel
            {
                Text = I18n.T(I18n.Text_List.About_FontLicense),
                Font = _fontManager.AppFontMedium,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, y),
                Size = new Size(width, linkHeight),
                // リンクの色はメイン画面の「更新」などのボタンと同じ色
                LinkColor = COLOR_PRIMARY,
                ActiveLinkColor = COLOR_PRIMARY,
                VisitedLinkColor = COLOR_PRIMARY
            };
            lnkLicense.LinkClicked += (s, e) => ShowLicense();
            this.Controls.Add(lnkLicense);
            y += linkHeight + S(16);

            // 下部: メイン画面と同じ 区切り線 / 余白 S(12) / ボタン / 余白 S(12)
            this.Controls.Add(new Label { Name = FOOTER_SEPARATOR_NAME, BorderStyle = BorderStyle.Fixed3D, Location = new Point(0, y), Size = new Size(width, S(2)) });
            y += S(2) + S(12);

            // OK ボタンはメイン画面の下部のボタン（MainForm.CreateButton）と同じ見た目
            int buttonHeight = Math.Max(S(35), TextRenderer.MeasureText("Ag", _fontManager.AppFontBold).Height + S(15));
            var btnOk = new Button
            {
                Text = "OK",
                Size = new Size(S(100), buttonHeight),
                Location = new Point((width - S(100)) / 2, y),
                BackColor = COLOR_PRIMARY,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = _fontManager.AppFontBold,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            this.Controls.Add(btnOk);
            this.AcceptButton = btnOk;
            this.CancelButton = btnOk;
            y += buttonHeight + S(12);

            this.ClientSize = new Size(width, y);
            ApplyTheme();
        }
    }
}
