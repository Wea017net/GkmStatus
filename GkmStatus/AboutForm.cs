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

            //SetupInitializeComponent();

            SuspendLayout();
            InitializeComponent();
            ApplyComponent();
            ResumeLayout();
        }

        private static readonly Color COLOR_PRIMARY_DEFAULT = ColorTranslator.FromHtml("#7e87f4");

        private void ShowLicense(object sender, LinkLabelLinkClickedEventArgs e)
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

        private void Close(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
