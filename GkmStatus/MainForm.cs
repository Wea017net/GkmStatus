using System;
using System.Drawing;
using System.Drawing.Text;
using System.Diagnostics;
using System.Windows.Forms;
using DiscordRPC;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text;
using System.Runtime.Versioning;
using Button = DiscordRPC.Button;
using Microsoft.Win32;

namespace GkmStatus
{
    public class AppConfig
    {
        public AppSettings Settings { get; set; } = new AppSettings();
        public PresenceSettings Presence { get; set; } = new PresenceSettings();
    }

    public class AppSettings
    {
        public bool StartMinimized { get; set; } = false;
        public bool ConnectOnStart { get; set; } = false;
        public bool AutoCheckUpdates { get; set; } = true;
        public bool ShowBackgroundNotifications { get; set; } = true;
        public bool NotifyOnMinimize { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool AutoDetectGakumas { get; set; } = true;
        public string SelectedTheme { get; set; } = "自動選択";
        public string SelectedLanguage { get; set; } = "日本語";
        public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;
    }

    public class PresenceSettings
    {
        public int DetailsTypeIndex { get; set; } = 3;
        public string ProducerName { get; set; } = "";
        public int ProducerLevel { get; set; } = 1;
        public int StateTypeIndex { get; set; } = -1;
        public string StateType { get; set; } = "Idol";
        public int CharNameLangIndex { get; set; } = 0;
        public string? SelectedProduceCharacterId { get; set; } = "hanami_saki";
        public Dictionary<string, string> StateHistory { get; set; } = [];
        public int GameAppIndex { get; set; } = 0;
        public int ButtonModeIndex { get; set; } = 0;
        public Dictionary<string, ButtonHistoryData> ButtonHistory { get; set; } = [];
    }

    public class ProduceCharacter
    {
        public string Id { get; set; } = "";
        public string Display { get; set; } = "";
        public string NameEn { get; set; } = "";
    }

    public class ButtonHistoryData
    {
        public string L1 { get; set; } = ""; public string U1 { get; set; } = "";
        public string L2 { get; set; } = ""; public string U2 { get; set; } = "";
    }

    [SupportedOSPlatform("windows")]
    public class MyRenderer(bool isBright, CustomColorTable colorTable) : ToolStripProfessionalRenderer(colorTable)
    {
        private readonly bool _isBright = isBright;
        private readonly CustomColorTable _colorTable = colorTable;

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var b = new SolidBrush(e.ToolStrip.BackColor);
            e.Graphics.FillRectangle(b, 0, 0, e.ToolStrip.Width, e.ToolStrip.Height);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using var b = new SolidBrush(e.ToolStrip.BackColor);
            e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Image != null)
            {
                e.Graphics.DrawImage(e.Image, e.ImageRectangle);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle r = new(Point.Empty, e.Item.Size);
            r.Width -= 1;
            r.Height -= 1;

            if (e.Item.Pressed)
            {
                using var b = new SolidBrush(_colorTable.HoverBgColor);
                e.Graphics.FillRectangle(b, r);

                Color borderColor = _isBright ? Color.Gray : Color.White;
                using var p = new Pen(borderColor, 1);
                e.Graphics.DrawRectangle(p, r);
            }
            else if (e.Item.Selected)
            {
                using var b = new SolidBrush(_colorTable.HoverBgColor);
                e.Graphics.FillRectangle(b, r);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = _isBright ? Color.Black : Color.White;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            Color line = _isBright ? Color.FromArgb(180, 180, 180) : Color.FromArgb(80, 80, 80);
            using var p = new Pen(line);
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(p, 30, y, e.Item.Width - 5, y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = _isBright ? Color.Black : Color.White;
            base.OnRenderArrow(e);
        }
    }

    [SupportedOSPlatform("windows")]
    public class CustomColorTable(bool isBright) : ProfessionalColorTable
    {
        public Color HoverBgColor { get; } = isBright ? Color.FromArgb(180, 200, 200, 200) : Color.FromArgb(60, 255, 255, 255);
        public Color CustomBorderColor { get; } = isBright ? Color.Gray : Color.White;

        public override Color MenuItemSelected => HoverBgColor;
        public override Color MenuItemSelectedGradientBegin => HoverBgColor;
        public override Color MenuItemSelectedGradientEnd => HoverBgColor;
        public override Color MenuItemPressedGradientBegin => HoverBgColor;
        public override Color MenuItemPressedGradientEnd => HoverBgColor;
        public override Color MenuItemPressedGradientMiddle => HoverBgColor;
        public override Color MenuItemBorder => CustomBorderColor;
        public override Color MenuBorder => CustomBorderColor;
    }

    [SupportedOSPlatform("windows")]
    public partial class MainForm : Form
    {
        [LibraryImport("user32.dll", EntryPoint = "SendMessageW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        [LibraryImport("dwmapi.dll")]
        private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [LibraryImport("gdi32.dll")]
        private static partial IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, out uint pcFonts);


        private const string PROCESS_NAME = "gakumas";
        private static readonly List<(string Name, string AppId)> GameApps =
        [
            ("学園アイドルマスター", "1352261574877778001"),
            ("学マス", "1467733389170835486"),
            ("THE IDOLM@STER Gakuen", "1467733691382890499"),
            ("Gakuen iDOLM@STER", "1467734377197867040"),
            ("Gakumas", "1467734892208193650")
        ];
        private const string REG_RUN_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "GkmStatus";
        private readonly string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

        private static readonly Color COLOR_ERROR = ColorTranslator.FromHtml("#fc5555");
        private static readonly Color COLOR_CONNECT = ColorTranslator.FromHtml("#68b900");
        private static readonly Color COLOR_PAUSE = ColorTranslator.FromHtml("#fc930f");
        private static readonly Color COLOR_PRIMARY = ColorTranslator.FromHtml("#7e87f4");
        private static readonly Color COLOR_TEXT_WHITE = Color.FromArgb(255, 255, 255);
        private static readonly Color COLOR_DISABLED = Color.FromArgb(60, 63, 65);

        private static readonly List<ProduceCharacter> ProduceCharacters =
        [
            new() { Id = "hanami_saki", Display = "花海咲季", NameEn = "Saki Hanami" },
            new() { Id = "tsukimura_temari", Display = "月村手毬", NameEn = "Temari Tsukimura" },
            new() { Id = "fujita_kotone", Display = "藤田ことね", NameEn = "Kotone Fujita" },
            new() { Id = "amaya_tsubame", Display = "雨夜燕", NameEn = "Tsubame Amaya" },
            new() { Id = "arimura_mao", Display = "有村麻央", NameEn = "Mao Arimura" },
            new() { Id = "katsuragi_lilja", Display = "葛城リーリヤ", NameEn = "Lilja Katsuragi" },
            new() { Id = "kuramoto_china", Display = "倉本千奈", NameEn = "China Kuramoto" },
            new() { Id = "shiun_sumika", Display = "紫雲清夏", NameEn = "Sumika Shiun" },
            new() { Id = "shinosawa_hiro", Display = "篠澤広", NameEn = "Hiro Shinosawa" },
            new() { Id = "juo_sena", Display = "十王星南", NameEn = "Sena Juo" },
            new() { Id = "hataya_misuzu", Display = "秦谷美鈴", NameEn = "Misuzu Hataya" },
            new() { Id = "hanami_ume", Display = "花海佑芽", NameEn = "Ume Hanami" },
            new() { Id = "himesaki_rinami", Display = "姫崎莉波", NameEn = "Rinami Himesaki" }
        ];

        private readonly bool isInitializing = true;

        private ComboBox cmbGameName = null!;
        private ComboBox cmbDetailsType = null!;
        private TextBox txtPName = null!;
        private NumericUpDown numPLevel = null!;
        private ComboBox cmbStateType = null!;
        private ComboBox cmbProduceCharacter = null!;
        private ComboBox cmbCharNameLang = null!;
        private TextBox txtStateCustom = null!;
        private Label lblStartTime = null!;
        private System.Windows.Forms.Button btnResetTime = null!;
        private ComboBox cmbBtnMode = null!;
        private TextBox txtBtn1Label = null!, txtBtn1Url = null!;
        private TextBox txtBtn2Label = null!, txtBtn2Url = null!;
        private System.Windows.Forms.Label lblStatus = null!;
        private Label lblGameAppGuide = null!;
        private System.Windows.Forms.Timer gameAppGuideTimer = null!;
        private System.Windows.Forms.Button btnConnect = null!, btnDisconnect = null!, btnUpdate = null!;
        private Label lblResetGuide = null!;
        private Label lblBtnWarning = null!;
        private ToolTip statusToolTip = null!;
        private System.Windows.Forms.Timer resetGuideTimer = null!;
        private int footerButtonsY;

        private ToolStripMenuItem fileMenu = null!, exitItem = null!, settingsMenu = null!, runAtStartupItem = null!, startMinimizedItem = null!, autoConnectItem = null!, checkForUpdatesItem = null!, notifyInBackgroundItem = null!, notifyOnMinimizeItem = null!, minimizeToTrayItem = null!, monitorItem = null!;
        private ToolStripMenuItem viewMenu = null!, themeMenu = null!, langMenu = null!, helpMenu = null!, githubMenu = null!, checkUpdateMenuItem = null!, aboutMenu = null!;
        private ToolStripMenuItem langEnglish = null!, langJapanese = null!;
        private ToolStripMenuItem themeAuto = null!, themeLight = null!, themeDark = null!, themeOLED = null!;
        private string currentThemeName = "自動選択";
        private string currentLanguage = "日本語";
        private ToolStripMenuItem trayMenuConnect = null!, trayMenuPause = null!, trayMenuDisconnect = null!, trayMenuProduce = null!, trayMenuDetails = null!, trayMenuState = null!;

        private DiscordRpcClient? client;
        private DateTime startTime;
        private System.Windows.Forms.Timer? monitorTimer;
        private System.Windows.Forms.Timer? connectionTimer;
        private int connectionSeconds = 0;
        private const int CONNECTION_TIMEOUT = 30;
        private bool wasProcessRunning = false;
        private Font appFont = null!;
        private Font appFontBold = null!;
        private Font appFontMedium = null!;
        private Font menuFont = null!;
        private Color defaultBackground;

        private int lastStateIdx = -1;

        private int lastBtnIdx = -1;
        private PresenceSettings currentPresence = new();
        private NotifyIcon trayIcon = null!;
        private Icon? currentTrayIconStatus;
        private DateTime lastUpdateCheck = DateTime.MinValue;
        private PrivateFontCollection? pfc;
        private readonly List<IntPtr> fontPointers = [];


        private float uiScale = 1.0f;
        private bool isInternalMinimize = false;

        public MainForm()
        {
            InitializeComponent();
            try
            {
                uiScale = (float)this.DeviceDpi / 96f;
                if (uiScale <= 0) uiScale = 1.0f;
            }
            catch
            {
                uiScale = 1.0f;
            }

            SetupFonts();
            InitializeCustomUI();
            InitializeLogic();

            isInitializing = true;
            LoadSettings();

            if (startMinimizedItem.Checked)
            {
                this.WindowState = FormWindowState.Minimized;
            }

            UpdateDetailsInputs(null, EventArgs.Empty);
            CmbStateType_SelectedIndexChanged(null, EventArgs.Empty);
            CmbBtnMode_SelectedIndexChanged(null, EventArgs.Empty);

            isInitializing = false;

            this.FormClosing += (s, e) =>
            {
                if (minimizeToTrayItem?.Checked == true && e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    isInternalMinimize = true;
                    this.WindowState = FormWindowState.Minimized;
                    isInternalMinimize = false;
                    return;
                }
                if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
                SaveSettings();
                foreach (var ptr in fontPointers) Marshal.FreeCoTaskMem(ptr);
                pfc?.Dispose();
            };

        }

        private int S(int value) => (int)Math.Round(value * uiScale);

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            float ratio = (float)e.DeviceDpiNew / e.DeviceDpiOld;
            uiScale = (float)e.DeviceDpiNew / 96f;

            base.OnDpiChanged(e);

            this.Scale(new SizeF(ratio, ratio));

            this.ClientSize = new Size(S(520), S(630));
            footerButtonsY = this.ClientSize.Height - S(80);

            SetupFonts();
            SetThemeColors(this.BackColor);
            AdjustStatusVerticalPosition();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (checkForUpdatesItem.Checked) _ = CheckForUpdatesAsync();
            if (startMinimizedItem.Checked)
            {
                this.Hide();
                trayIcon.Visible = true;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (this.WindowState == FormWindowState.Minimized)
            {
                if (minimizeToTrayItem?.Checked == true && !isInternalMinimize) return;

                this.Hide();
                trayIcon.Visible = true;
                if (notifyOnMinimizeItem?.Checked == true)
                {
                    trayIcon.Tag = null;
                    trayIcon.ShowBalloonTip(2000, I18n.T("App_Name"), I18n.T("Notify_Minimized"), ToolTipIcon.Info);
                }
            }
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            trayIcon.Visible = false;
            this.Activate();
            ReapplyTheme();
        }

        private void ReapplyTheme()
        {
            switch (currentThemeName)
            {
                case "ライト": ApplyThemeLight(); break;
                case "ダーク": ApplyThemeDark(); break;
                case "OLED": ApplyThemeOLED(); break;
                default: ApplyThemeAuto(); break;
            }
        }

        private void SetupFonts()
        {
            try
            {
                if (pfc == null)
                {
                    pfc = new PrivateFontCollection();
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                    try { Debug.WriteLine("Manifest resources: " + string.Join(", ", assembly.GetManifestResourceNames())); } catch { }

                    var manifestNames = assembly.GetManifestResourceNames();
                    var ttfCandidates = manifestNames.Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) && n.Contains("plex", StringComparison.OrdinalIgnoreCase)).ToArray();

                    if (ttfCandidates.Length == 0)
                    {
                        ttfCandidates = [
                            "GkmStatus.Resources.fonts.IBM_Plex_Sans_JP.IBMPlexSansJP-Regular.ttf",
                            "GkmStatus.Resources.fonts.IBM_Plex_Sans_JP.IBMPlexSansJP-Bold.ttf",
                            "GkmStatus.Resources.fonts.IBM_Plex_Sans_JP.IBMPlexSansJP-Medium.ttf"
                        ];
                    }

                    foreach (string resourceName in ttfCandidates)
                    {
                        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                        if (stream != null)
                        {
                            try
                            {
                                byte[] fontData = new byte[stream.Length];
                                stream.ReadExactly(fontData, 0, (int)stream.Length);
                                IntPtr fontPtr = Marshal.AllocCoTaskMem(fontData.Length);
                                Marshal.Copy(fontData, 0, fontPtr, fontData.Length);

                                pfc.AddMemoryFont(fontPtr, fontData.Length);

                                AddFontMemResourceEx(fontPtr, (uint)fontData.Length, IntPtr.Zero, out uint _);

                                fontPointers.Add(fontPtr);
                                Debug.WriteLine("Loaded and registered font resource: " + resourceName);
                            }
                            catch (Exception ex) { Debug.WriteLine("Failed loading font resource '" + resourceName + "': " + ex.Message); }
                        }
                        else
                        {
                            Debug.WriteLine("Font resource not found: " + resourceName);
                        }
                    }
                }

                FontFamily? regular = null, bold = null, medium = null;
                try { Debug.WriteLine("PrivateFontCollection families: " + string.Join(", ", pfc.Families.Select(f => f.Name))); } catch { }
                foreach (var ff in pfc.Families)
                {
                    string name = ff.Name;
                    if (name.Contains("IBM Plex Sans JP", StringComparison.OrdinalIgnoreCase))
                    {
                        if (name.Contains("Bold", StringComparison.OrdinalIgnoreCase)) bold = ff;
                        else if (name.Contains("Medium", StringComparison.OrdinalIgnoreCase)) medium = ff;
                        else regular = ff;
                    }
                }

                if (regular == null && pfc.Families.Length > 0)
                {
                    regular = pfc.Families.FirstOrDefault(f => f.Name.Contains("IBM Plex Sans JP", StringComparison.OrdinalIgnoreCase))
                              ?? pfc.Families[0];
                }

                if (regular != null)
                {
                    float basePx = 13.3f * uiScale;
                    appFont = new Font(regular, basePx, GraphicsUnit.Pixel);

                    if (bold != null) appFontBold = new Font(bold, basePx, GraphicsUnit.Pixel);
                    else appFontBold = new Font(regular, basePx, FontStyle.Bold, GraphicsUnit.Pixel);

                    if (medium != null) appFontMedium = new Font(medium, basePx, GraphicsUnit.Pixel);
                    else appFontMedium = appFontBold;

                    SetupMenuFont();
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Font Load Error: " + ex.Message);
            }


            float basePxFallback = 13.3f * uiScale;
            appFont = new Font("Meiryo UI", basePxFallback, GraphicsUnit.Pixel);
            appFontBold = new Font("Meiryo UI", basePxFallback, FontStyle.Bold, GraphicsUnit.Pixel);
            appFontMedium = new Font("Meiryo UI", basePxFallback, GraphicsUnit.Pixel);

            SetupMenuFont();
        }

        private void SetupMenuFont()
        {
            float menuPx = 12f * uiScale;
            try
            {
                using var testFont = new Font("Yu Gothic UI", menuPx, GraphicsUnit.Pixel);
                if (testFont.Name == "Yu Gothic UI")
                {
                    menuFont = new Font("Yu Gothic UI", menuPx, GraphicsUnit.Pixel);
                }
                else
                {
                    var family = SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif;
                    menuFont = new Font(family, menuPx, GraphicsUnit.Pixel);
                }
            }
            catch
            {
                var family = SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif;
                menuFont = new Font(family, menuPx, GraphicsUnit.Pixel);
            }
        }

        private void InitializeCustomUI()
        {
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;

            this.ClientSize = new Size(S(520), S(630));
            this.FormBorderStyle = FormBorderStyle.FixedSingle;

            this.Text = I18n.T("App_Name");
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(32, 34, 37);
            this.ForeColor = Color.White;
            this.Font = appFont;
            defaultBackground = this.BackColor;

            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using Stream? stream = assembly.GetManifestResourceStream("GkmStatus.Resources.app.ico");
                if (stream != null)
                {
                    this.Icon = new Icon(stream);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Icon Load Error: " + ex.Message);
            }

            trayIcon = new NotifyIcon
            {
                Icon = this.Icon ?? SystemIcons.Application,
                Text = I18n.T("App_Name"),
                Visible = false
            };

            trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) RestoreFromTray();
            };
            trayIcon.BalloonTipClicked += (s, e) =>
            {
                if (trayIcon.Tag is string url && !string.IsNullOrEmpty(url))
                {
                    try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
                    trayIcon.Tag = null;
                }
                else
                {
                    this.Invoke((MethodInvoker)(() => RestoreFromTray()));
                }
            };

            var trayMenu = new ContextMenuStrip();
            trayMenu.Opening += (s, e) => { if (trayMenuProduce != null) trayMenuProduce.Enabled = (cmbStateType.SelectedIndex == 3); };
            trayMenu.Items.Add(I18n.T("Tray_Open"), null, (s, e) => RestoreFromTray());
            trayMenu.Items.Add(new ToolStripSeparator());

            trayMenuDetails = new ToolStripMenuItem(I18n.T("Header_Details"));
            trayMenuDetails.DropDownItems.Add(new ToolStripMenuItem("..."));
            trayMenuDetails.DropDownOpening += (s, e) =>
            {
                trayMenuDetails.DropDownItems.Clear();
                for (int i = 0; i < cmbDetailsType.Items.Count; i++)
                {
                    string text = cmbDetailsType.Items[i]?.ToString() ?? "";
                    var item = new ToolStripMenuItem(text);
                    if (cmbDetailsType.SelectedIndex == i) item.Checked = true;
                    int index = i;
                    item.Click += (sender, ev) =>
                    {
                        cmbDetailsType.SelectedIndex = index;
                        UpdateRpc();
                        if (notifyInBackgroundItem?.Checked == true)
                        {
                            trayIcon.Tag = null;
                            trayIcon.ShowBalloonTip(3000, I18n.T("App_Name"), I18n.T("Notify_TrayDetailsChanged"), ToolTipIcon.Info);
                        }
                    };
                    trayMenuDetails.DropDownItems.Add(item);
                }
            };
            trayMenu.Items.Add(trayMenuDetails);

            trayMenuState = new ToolStripMenuItem(I18n.T("Header_State"));
            trayMenuState.DropDownItems.Add(new ToolStripMenuItem("..."));
            trayMenuState.DropDownOpening += (s, e) =>
            {
                trayMenuState.DropDownItems.Clear();
                for (int i = 0; i < cmbStateType.Items.Count; i++)
                {
                    string text = cmbStateType.Items[i]?.ToString() ?? "";
                    var item = new ToolStripMenuItem(text);
                    if (cmbStateType.SelectedIndex == i) item.Checked = true;
                    int index = i;
                    item.Click += (sender, ev) =>
                    {
                        cmbStateType.SelectedIndex = index;
                        UpdateRpc();
                        if (notifyInBackgroundItem?.Checked == true)
                        {
                            trayIcon.Tag = null;
                            trayIcon.ShowBalloonTip(3000, I18n.T("App_Name"), I18n.T("Notify_TrayStateChanged"), ToolTipIcon.Info);
                        }
                    };
                    trayMenuState.DropDownItems.Add(item);
                }
            };
            trayMenu.Items.Add(trayMenuState);

            trayMenuProduce = new ToolStripMenuItem(I18n.T("Tray_ProducingIdol"));
            trayMenuProduce.DropDownItems.Add(new ToolStripMenuItem("..."));
            trayMenuProduce.DropDownOpening += (s, e) =>
            {
                trayMenuProduce.DropDownItems.Clear();
                foreach (var pc in ProduceCharacters)
                {
                    string displayName = currentPresence.CharNameLangIndex == 1 ? pc.NameEn : pc.Display;
                    var item = new ToolStripMenuItem(displayName);
                    if (currentPresence.SelectedProduceCharacterId == pc.Id) item.Checked = true;
                    item.Click += (sender, ev) =>
                    {
                        currentPresence.SelectedProduceCharacterId = pc.Id;
                        int index = ProduceCharacters.IndexOf(pc);
                        if (index >= 0 && index < cmbProduceCharacter.Items.Count) cmbProduceCharacter.SelectedIndex = index;
                        UpdateRpc();

                        if (notifyInBackgroundItem?.Checked == true)
                        {
                            trayIcon.Tag = null;
                            trayIcon.ShowBalloonTip(3000, I18n.T("App_Name"), I18n.T("Notify_TrayIdolChanged"), ToolTipIcon.Info);
                        }
                    };
                    trayMenuProduce.DropDownItems.Add(item);
                }
            };
            trayMenu.Items.Add(trayMenuProduce);
            trayMenu.Items.Add(new ToolStripSeparator());

            trayMenuConnect = new ToolStripMenuItem(I18n.T("Button_Connect"), null, (s, e) => InitializeRpc());
            trayMenuPause = new ToolStripMenuItem(I18n.T("Button_Pause"), null, (s, e) => PauseRpc());
            trayMenuDisconnect = new ToolStripMenuItem(I18n.T("Button_Disconnect"), null, (s, e) => DisposeRpc());

            trayMenu.Items.AddRange([trayMenuConnect, trayMenuPause, trayMenuDisconnect]);

            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(I18n.T("Tray_Exit"), null, (s, e) => Application.Exit());
            trayIcon.ContextMenuStrip = trayMenu;

            int y = S(40);
            void AddHeader(string text, int top, string tag)
            {
                var lbl = new Label { Text = text, Location = new Point(S(20), top), AutoSize = true, Font = appFontBold, ForeColor = Color.LightGray, Tag = tag };
                this.Controls.Add(lbl);
            }

            AddHeader(I18n.T("Header_GameName"), y, "Header_GameName"); y += S(25);
            cmbGameName = CreateCombo(new(S(20), y), S(210), [.. GameApps.Select(g => g.Name)]);
            cmbGameName.SelectedIndex = 0;
            cmbGameName.SelectedIndexChanged += CmbGameName_SelectedIndexChanged;

            lblGameAppGuide = new Label
            {
                Text = I18n.T("GameApp_Guide"),
                Location = new Point(S(240), y - S(7)),
                AutoSize = true,
                Visible = false,
                ForeColor = COLOR_PAUSE,
                Font = appFontMedium
            };
            this.Controls.Add(lblGameAppGuide);

            gameAppGuideTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            gameAppGuideTimer.Tick += (s, e) =>
            {
                lblGameAppGuide.Visible = false;
                gameAppGuideTimer.Stop();
            };

            y += S(45);

            AddHeader(I18n.T("Header_Details"), y, "Header_Details"); y += S(25);
            cmbDetailsType = CreateCombo(new Point(S(20), y), S(180), [I18n.T("Details_None"), I18n.T("Details_PName"), I18n.T("Details_PLv"), I18n.T("Details_Both")]);
            cmbDetailsType.SelectedIndex = 3; cmbDetailsType.SelectedIndexChanged += UpdateDetailsInputs;
            txtPName = CreateText(new Point(S(210), y), S(200)); SetPlaceholder(txtPName, I18n.T("Placeholder_PName"));
            txtPName.MaxLength = 10;
            txtPName.TextChanged += (s, e) => { if (!isInitializing) SaveSettings(); };
            numPLevel = CreateNumeric(new Point(S(420), y), S(80));
            numPLevel.ValueChanged += (s, e) => { if (!isInitializing) SaveSettings(); };
            y += S(45);

            AddHeader(I18n.T("Header_State"), y, "Header_State"); y += S(25);

            cmbStateType = CreateCombo(
                new(S(20), y),
                S(150),
                [I18n.T("State_None"), I18n.T("State_PID"), I18n.T("State_Idol"), I18n.T("State_Producing"), I18n.T("State_Custom")]
            );

            cmbProduceCharacter = CreateCombo(
                new(S(180), y),
                S(165),
                []
            );
            cmbProduceCharacter.Visible = false;

            cmbCharNameLang = CreateCombo(
                new Point(S(350), y),
                S(150),
                [I18n.T("CharName_JP"), I18n.T("CharName_EN")]
            );
            cmbCharNameLang.Visible = false;
            cmbCharNameLang.SelectedIndex = 0;
            cmbCharNameLang.SelectedIndexChanged += (s, e) =>
            {
                if (!isInitializing)
                {
                    currentPresence.CharNameLangIndex = cmbCharNameLang.SelectedIndex;
                    RefreshProduceCharacterList();
                    SaveSettings();
                }
            };

            foreach (var c in ProduceCharacters)
            {
                cmbProduceCharacter.Items.Add(c.Display);
            }
            cmbProduceCharacter.SelectedIndex = 0;

            cmbProduceCharacter.SelectedIndexChanged += (s, e) =>
            {
                if (cmbProduceCharacter.SelectedIndex >= 0)
                {
                    currentPresence.SelectedProduceCharacterId = ProduceCharacters[cmbProduceCharacter.SelectedIndex].Id;
                    if (!isInitializing) SaveSettings();
                }
            };

            txtStateCustom = CreateText(new Point(S(180), y), S(320)); txtStateCustom.Enabled = false;
            cmbStateType.SelectedIndexChanged += CmbStateType_SelectedIndexChanged;
            y += S(45);

            AddHeader(I18n.T("Header_Timestamp"), y, "Header_Timestamp"); y += S(25);
            lblStartTime = new Label { Text = I18n.T("Timestamp_Label") + ": 00:00:00", Location = new Point(S(20), y + S(7)), AutoSize = true };
            this.Controls.Add(lblStartTime);
            btnResetTime = CreateButton(I18n.T("Timestamp_Reset"), new Point(S(170), y), S(120), COLOR_PRIMARY);
            btnResetTime.Click += (s, e) =>
            {
                startTime = DateTime.UtcNow;
                UpdateTimestampLabel();
                lblResetGuide.Visible = true;
                resetGuideTimer.Stop();
                resetGuideTimer.Start();
                if (client?.IsInitialized == true) UpdateRpc();
            };
            y += S(45);

            lblResetGuide = new Label
            {
                Text = I18n.T("Timestamp_Guide"),
                Location = new Point(S(300), y - S(38)),
                AutoSize = true,
                Visible = false,
                ForeColor = Color.Gray,
                Font = appFontMedium
            };
            this.Controls.Add(lblResetGuide);

            resetGuideTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            resetGuideTimer.Tick += (s, e) =>
            {
                lblResetGuide.Visible = false;
                resetGuideTimer.Stop();
            };

            AddHeader(I18n.T("Header_Buttons"), y, "Header_Buttons"); y += S(25);
            cmbBtnMode = CreateCombo(new Point(S(20), y), S(150), [I18n.T("Button_ModeNone"), I18n.T("Button_ModeStore"), I18n.T("Button_ModeApp"), I18n.T("Button_ModeCustom")]);
            cmbBtnMode.SelectedIndexChanged += CmbBtnMode_SelectedIndexChanged;
            y += S(35);
            txtBtn1Label = CreateText(new Point(S(20), y), S(150)); txtBtn1Label.MaxLength = 31;
            txtBtn1Url = CreateText(new Point(S(180), y), S(320)); txtBtn1Url.MaxLength = 512;
            y += S(35);
            txtBtn2Label = CreateText(new Point(S(20), y), S(150)); txtBtn2Label.MaxLength = 31;
            txtBtn2Url = CreateText(new Point(S(180), y), S(320)); txtBtn2Url.MaxLength = 512;

            lblBtnWarning = new Label
            {
                Text = "",
                Location = new Point(S(20), y + S(40)),
                AutoSize = true,
                Visible = false,
                ForeColor = COLOR_PAUSE,
                Font = appFontMedium
            };
            this.Controls.Add(lblBtnWarning);

            void CheckBtnWarning(object? sender, EventArgs e)
            {
                bool labelOver = Encoding.UTF8.GetByteCount(txtBtn1Label.Text) > 32 || Encoding.UTF8.GetByteCount(txtBtn2Label.Text) > 32;
                bool urlJapanese = JapaneseRegex().IsMatch(txtBtn1Url.Text + txtBtn2Url.Text);

                var sb = new StringBuilder();
                if (labelOver) sb.AppendLine(I18n.T("Button_Warning_LabelLength"));
                if (urlJapanese) sb.Append(I18n.T("Button_Warning_UrlJp"));

                string msg = sb.ToString().Trim();
                lblBtnWarning.Text = msg;
                lblBtnWarning.Visible = !string.IsNullOrEmpty(msg);

                if (urlJapanese) lblBtnWarning.ForeColor = COLOR_ERROR;
                else lblBtnWarning.ForeColor = COLOR_PAUSE;
            }
            txtBtn1Label.TextChanged += CheckBtnWarning;
            txtBtn2Label.TextChanged += CheckBtnWarning;
            txtBtn1Url.TextChanged += CheckBtnWarning;
            txtBtn2Url.TextChanged += CheckBtnWarning;

            y += S(50);

            y = this.ClientSize.Height - S(80);
            footerButtonsY = y;
            this.Controls.Add(new Label { BorderStyle = BorderStyle.Fixed3D, Location = new Point(0, y), Size = new Size(S(520), S(2)) });
            y += S(20);

            btnConnect = CreateButton(I18n.T("Button_Connect"), new Point(S(20), y), S(100), COLOR_CONNECT);
            btnConnect.Click += (s, e) => { if (btnConnect.Text == I18n.T("Button_Pause")) PauseRpc(); else InitializeRpc(); };
            btnUpdate = CreateButton(I18n.T("Button_Update"), new Point(S(130), y), S(100), COLOR_PRIMARY);
            btnUpdate.Enabled = false; btnUpdate.Click += async (s, e) =>
            {
                UpdateRpc();
                string originalText = lblStatus.Text;
                Color originalColor = lblStatus.ForeColor;
                lblStatus.Text = I18n.T("Status_Updated");
                lblStatus.ForeColor = COLOR_PRIMARY;
                AdjustStatusVerticalPosition();

                await System.Threading.Tasks.Task.Delay(2000);

                if (this.IsDisposed) return;
                if (lblStatus.Text == I18n.T("Status_Updated"))
                {
                    lblStatus.Text = originalText;
                    lblStatus.ForeColor = originalColor;
                    AdjustStatusVerticalPosition();
                }
            };
            btnDisconnect = CreateButton(I18n.T("Button_Disconnect"), new Point(S(240), y), S(100), COLOR_ERROR);
            btnDisconnect.Enabled = false; btnDisconnect.Click += (s, e) => DisposeRpc();

            lblStatus = new Label { Text = I18n.T("Status_Disconnected"), Location = new Point(S(360), y), AutoSize = true, ForeColor = Color.Gray, Font = appFontMedium };
            statusToolTip = new ToolTip();
            statusToolTip.SetToolTip(lblStatus, lblStatus.Text);
            this.Controls.Add(btnConnect); this.Controls.Add(btnUpdate); this.Controls.Add(btnDisconnect); this.Controls.Add(lblStatus);

            AdjustStatusVerticalPosition();

            UpdateUIForDisconnect();

            var menu = new MenuStrip();
            fileMenu = new(I18n.T("Menu_File"));
            exitItem = new(I18n.T("Menu_Exit")) { ShortcutKeyDisplayString = "Alt+F4" };
            exitItem.Click += (s, e) => { Application.Exit(); };
            fileMenu.DropDownItems.Add(exitItem);

            settingsMenu = new(I18n.T("Menu_Settings"));
            runAtStartupItem = new(I18n.T("Menu_RunAtStartup")) { CheckOnClick = true, Checked = IsRunAtStartup() };
            runAtStartupItem.CheckedChanged += (s, e) => SetRunAtStartup(runAtStartupItem.Checked);

            startMinimizedItem = new(I18n.T("Menu_StartMinimized")) { CheckOnClick = true };
            startMinimizedItem.CheckedChanged += (s, e) => SaveSettings();

            autoConnectItem = new(I18n.T("Menu_AutoConnect")) { CheckOnClick = true };
            autoConnectItem.CheckedChanged += (s, e) => SaveSettings();

            checkForUpdatesItem = new(I18n.T("Menu_CheckForUpdates")) { CheckOnClick = true, Checked = true };
            checkForUpdatesItem.CheckedChanged += (s, e) => SaveSettings();

            notifyInBackgroundItem = new(I18n.T("Menu_NotifyInBackground")) { CheckOnClick = true, Checked = true };
            notifyInBackgroundItem.CheckedChanged += (s, e) => SaveSettings();

            notifyOnMinimizeItem = new(I18n.T("Menu_NotifyOnMinimize")) { CheckOnClick = true, Checked = true };
            notifyOnMinimizeItem.CheckedChanged += (s, e) => SaveSettings();

            minimizeToTrayItem = new(I18n.T("Menu_MinimizeToTray")) { CheckOnClick = true, Checked = true };
            minimizeToTrayItem.CheckedChanged += (s, e) => SaveSettings();

            monitorItem = new(I18n.T("Menu_MonitorProcess")) { CheckOnClick = true, Checked = true };
            monitorItem.CheckedChanged += (s, e) => { if (monitorTimer != null) monitorTimer.Enabled = monitorItem.Checked; SaveSettings(); };

            ToolStripSeparator separatorSettings = new();
            settingsMenu.DropDownItems.AddRange([
                runAtStartupItem,
                startMinimizedItem,
                autoConnectItem,
                checkForUpdatesItem,
                separatorSettings,
                monitorItem,
                notifyInBackgroundItem,
                notifyOnMinimizeItem,
                minimizeToTrayItem
            ]);

            viewMenu = new ToolStripMenuItem(I18n.T("Menu_View"));
            themeMenu = new ToolStripMenuItem(I18n.T("Menu_Theme"));
            themeAuto = new ToolStripMenuItem(I18n.T("Menu_ThemeAuto"));
            themeLight = new ToolStripMenuItem("ライト");
            themeDark = new ToolStripMenuItem("ダーク");
            themeOLED = new ToolStripMenuItem("OLED");

            themeAuto.Click += (s, e) => { currentThemeName = "自動選択"; ApplyThemeAuto(); UpdateThemeChecks(themeAuto, themeLight, themeDark, themeOLED); SaveSettings(); };
            themeLight.Click += (s, e) => { currentThemeName = "ライト"; ApplyThemeLight(); UpdateThemeChecks(themeLight, themeAuto, themeDark, themeOLED); SaveSettings(); };
            themeDark.Click += (s, e) => { currentThemeName = "ダーク"; ApplyThemeDark(); UpdateThemeChecks(themeDark, themeAuto, themeLight, themeOLED); SaveSettings(); };
            themeOLED.Click += (s, e) => { currentThemeName = "OLED"; ApplyThemeOLED(); UpdateThemeChecks(themeOLED, themeAuto, themeLight, themeDark); SaveSettings(); };

            themeMenu.DropDownItems.AddRange([themeAuto, themeLight, themeDark, themeOLED]);
            viewMenu.DropDownItems.Add(themeMenu);

            var separator2 = new ToolStripSeparator();
            langMenu = new ToolStripMenuItem(I18n.T("Menu_Language"));
            langEnglish = new ToolStripMenuItem("English");
            langJapanese = new ToolStripMenuItem("日本語");

            langEnglish.Click += (s, e) => { currentLanguage = "English"; I18n.CurrentLanguage = "English"; UpdateThemeChecks(langEnglish, langJapanese); ApplyLanguage(); SaveSettings(); };
            langJapanese.Click += (s, e) => { currentLanguage = "日本語"; I18n.CurrentLanguage = "日本語"; UpdateThemeChecks(langJapanese, langEnglish); ApplyLanguage(); SaveSettings(); };

            langMenu.DropDownItems.AddRange([langEnglish, langJapanese]);
            viewMenu.DropDownItems.Add(separator2);
            viewMenu.DropDownItems.Add(langMenu);

            helpMenu = new ToolStripMenuItem(I18n.T("Menu_Help"));
            githubMenu = new ToolStripMenuItem(I18n.T("Menu_Github"));
            githubMenu.Click += (s, e) => { try { Process.Start(new ProcessStartInfo("https://github.com/Wea017net/GkmStatus") { UseShellExecute = true }); } catch { MessageBox.Show(I18n.T("Error_Browser")); } };

            checkUpdateMenuItem = new ToolStripMenuItem(I18n.T("Menu_CheckUpdateNow"));
            checkUpdateMenuItem.Click += (s, e) => _ = CheckForUpdatesAsync(manual: true);

            aboutMenu = new ToolStripMenuItem(I18n.T("Menu_About"));
            aboutMenu.Click += (s, e) => ShowAboutDialog();

            helpMenu.DropDownItems.AddRange([githubMenu, checkUpdateMenuItem, aboutMenu]);

            menu.Items.Add(fileMenu); menu.Items.Add(settingsMenu); menu.Items.Add(viewMenu); menu.Items.Add(helpMenu);
            this.Controls.Add(menu);

            ApplyLanguage();
            UpdateTrayMenuState();
        }

        private void ApplyLanguage()
        {
            this.Text = $"{I18n.T("App_Name")} v{Application.ProductVersion}";

            if (fileMenu != null)
            {
                fileMenu.Text = I18n.T("Menu_File");
                exitItem.Text = I18n.T("Menu_Exit");

                settingsMenu.Text = I18n.T("Menu_Settings");
                runAtStartupItem.Text = I18n.T("Menu_RunAtStartup");
                startMinimizedItem.Text = I18n.T("Menu_StartMinimized");
                autoConnectItem.Text = I18n.T("Menu_AutoConnect");
                checkForUpdatesItem.Text = I18n.T("Menu_CheckForUpdates");
                notifyInBackgroundItem.Text = I18n.T("Menu_NotifyInBackground");
                notifyOnMinimizeItem.Text = I18n.T("Menu_NotifyOnMinimize");
                minimizeToTrayItem.Text = I18n.T("Menu_MinimizeToTray");
                monitorItem.Text = I18n.T("Menu_MonitorProcess");

                viewMenu.Text = I18n.T("Menu_View");
                themeMenu.Text = I18n.T("Menu_Theme");
                themeAuto.Text = I18n.T("Menu_ThemeAuto");
                themeLight.Text = I18n.T("Menu_ThemeLight");
                themeDark.Text = I18n.T("Menu_ThemeDark");
                themeOLED.Text = I18n.T("Menu_ThemeOLED");
                langMenu.Text = I18n.T("Menu_Language");

                helpMenu.Text = I18n.T("Menu_Help");
                githubMenu.Text = I18n.T("Menu_Github");
                checkUpdateMenuItem.Text = I18n.T("Menu_CheckUpdateNow");
                aboutMenu.Text = I18n.T("Menu_About");
            }

            foreach (Control c in this.Controls)
            {
                if (c is Label lbl && lbl.Tag is string tag)
                {
                    if (tag.StartsWith("Header_")) lbl.Text = I18n.T(tag);
                }
            }

            if (trayIcon.ContextMenuStrip != null)
            {
                trayIcon.ContextMenuStrip.Items[0].Text = I18n.T("Tray_Open");
                if (trayMenuDetails != null) trayMenuDetails.Text = I18n.T("Header_Details");
                if (trayMenuState != null) trayMenuState.Text = I18n.T("Header_State");
                if (trayMenuProduce != null) trayMenuProduce.Text = I18n.T("Tray_ProducingIdol");
                trayMenuConnect.Text = I18n.T("Button_Connect");
                trayMenuPause.Text = I18n.T("Button_Pause");
                trayMenuDisconnect.Text = I18n.T("Button_Disconnect");
                if (trayIcon.ContextMenuStrip.Items.Count > 0) trayIcon.ContextMenuStrip.Items[trayIcon.ContextMenuStrip.Items.Count - 1].Text = I18n.T("Tray_Exit");
            }

            lblResetGuide.Text = I18n.T("Timestamp_Guide");
            lblGameAppGuide.Text = I18n.T("GameApp_Guide");
            UpdateTimestampLabel();

            btnResetTime.Text = I18n.T("Timestamp_Reset");
            btnUpdate.Text = I18n.T("Button_Update");
            btnDisconnect.Text = I18n.T("Button_Disconnect");

            SetPlaceholder(txtPName, I18n.T("Placeholder_PName"));
            SetPlaceholder(txtBtn1Label, I18n.T("Placeholder_BtnLabel", 1));
            SetPlaceholder(txtBtn1Url, I18n.T("Placeholder_BtnUrl", 1));
            SetPlaceholder(txtBtn2Label, I18n.T("Placeholder_BtnLabel", 2));
            SetPlaceholder(txtBtn2Url, I18n.T("Placeholder_BtnUrl", 2));

            if (client?.IsInitialized == true)
            {
                if (btnConnect.Text == I18n.T("Button_Pause"))
                {
                    UpdateUIForConnected(client.CurrentUser?.Username ?? "Unknown");
                }
                else
                {
                    UpdateUIForPause();
                }
            }
            else
            {
                UpdateUIForDisconnect();
            }

            static void RefreshCombo(ComboBox cb, string[] items)
            {
                int idx = cb.SelectedIndex;
                cb.Items.Clear();
                cb.Items.AddRange(items);
                cb.SelectedIndex = Math.Min(idx, cb.Items.Count - 1);
            }

            RefreshCombo(cmbDetailsType, [I18n.T("Details_None"), I18n.T("Details_PName"), I18n.T("Details_PLv"), I18n.T("Details_Both")]);
            RefreshCombo(cmbStateType, [I18n.T("State_None"), I18n.T("State_PID"), I18n.T("State_Idol"), I18n.T("State_Producing"), I18n.T("State_Custom")]);
            RefreshCombo(cmbBtnMode, [I18n.T("Button_ModeNone"), I18n.T("Button_ModeStore"), I18n.T("Button_ModeApp"), I18n.T("Button_ModeCustom")]);
            RefreshCombo(cmbCharNameLang, [I18n.T("CharName_JP"), I18n.T("CharName_EN")]);

            CmbStateType_SelectedIndexChanged(null, EventArgs.Empty);
            CmbBtnMode_SelectedIndexChanged(null, EventArgs.Empty);
        }

        private void CmbGameName_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbGameName.SelectedIndex == -1) return;
            currentPresence.GameAppIndex = cmbGameName.SelectedIndex;

            if (!isInitializing)
            {
                lblGameAppGuide.Visible = true;
                gameAppGuideTimer.Stop();
                gameAppGuideTimer.Start();
                SaveSettings();
            }
        }

        private void ShowAboutDialog()
        {
            using var about = new AboutForm(this.BackColor, this.ForeColor, appFont, uiScale);
            about.ShowDialog(this);
        }

        private static string NormalizeVersionString(string version)
        {
            if (string.IsNullOrEmpty(version)) return version;
            version = version.Trim();
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                version = version[1..];
            else if (version.StartsWith("Ver.", StringComparison.OrdinalIgnoreCase))
                version = version[4..];
            else if (version.StartsWith("Ver", StringComparison.OrdinalIgnoreCase))
                version = version[3..];
            return version.Trim();
        }

        private async Task CheckForUpdatesAsync(bool manual = false)
        {
            if (!manual && (DateTime.UtcNow - lastUpdateCheck).TotalHours < 24)
            {
                return;
            }

            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("GkmStatus-UpdateChecker");
                var response = await client.GetAsync("https://api.github.com/repos/Wea017net/GkmStatus/releases/latest");

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    if (manual) this.Invoke(() => MessageBox.Show(this, I18n.T("Update_RateLimit"), "Update", MessageBoxButtons.OK, MessageBoxIcon.Warning));
                    return;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    if (manual) this.Invoke(() => MessageBox.Show(this, I18n.T("Update_NoUpdate"), "Update", MessageBoxButtons.OK, MessageBoxIcon.Information));
                    lastUpdateCheck = DateTime.UtcNow;
                    SaveSettings();
                    return;
                }

                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                {
                    string latestStr = tag.GetString()?.TrimStart('v') ?? "";
                    string currentStr = NormalizeVersionString(Application.ProductVersion);
                    if (Version.TryParse(latestStr, out var latest) && Version.TryParse(currentStr, out var current))
                    {
                        lastUpdateCheck = DateTime.UtcNow;
                        SaveSettings();

                        if (latest > current)
                        {
                            this.Invoke(() =>
                            {
                                if (manual)
                                {
                                    if (MessageBox.Show(this, I18n.T("Update_NewAvailable", latestStr), "Update", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                                    {
                                        try { Process.Start(new ProcessStartInfo("https://github.com/Wea017net/GkmStatus/releases/latest") { UseShellExecute = true }); } catch { }
                                    }
                                }
                                else
                                {
                                    trayIcon.Visible = true;
                                    trayIcon.Tag = "https://github.com/Wea017net/GkmStatus/releases/latest";
                                    trayIcon.ShowBalloonTip(5000, I18n.T("Update_NotificationTitle"), I18n.T("Update_NotificationBody", latestStr), ToolTipIcon.Info);
                                }
                            });
                        }
                        else if (manual)
                        {
                            this.Invoke(() => MessageBox.Show(this, I18n.T("Update_NoUpdate"), "Update", MessageBoxButtons.OK, MessageBoxIcon.Information));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (manual) this.Invoke(() => MessageBox.Show(this, "Update check failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
            }
        }

        private static string GetStateString(int i) => i switch { 0 => "None", 1 => "PID", 2 => "Idol", 3 => "Producing", 4 => "Custom", _ => "PID" };
        private static int GetStateIndex(string s) => s switch { "None" => 0, "PID" => 1, "Idol" => 2, "Producing" => 3, "Custom" => 4, _ => 1 };

        private void CmbStateType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int idx = cmbStateType.SelectedIndex;
            if (idx == -1) return;

            if (!isInitializing && lastStateIdx != -1 && lastStateIdx != 2 && lastStateIdx != 3)
            {
                currentPresence.StateHistory[GetStateString(lastStateIdx)] = txtStateCustom.Text;
            }

            bool isProduceMode = (idx == 2 || idx == 3);

            if (txtStateCustom.Tag is Panel container)
            {
                container.Visible = !isProduceMode && (idx != 0);
            }

            cmbProduceCharacter.Visible = isProduceMode;
            cmbCharNameLang.Visible = isProduceMode;

            if (idx == 1)
            {
                if (txtStateCustom.Tag is Panel con)
                {
                    con.Width = S(165);
                    txtStateCustom.Width = S(165) - S(10);
                    con.Invalidate();
                }
                txtStateCustom.Enabled = true;
                txtStateCustom.MaxLength = 8;
                SetPlaceholder(txtStateCustom, I18n.T("Placeholder_PID"));
            }
            else if (idx == 4)
            {
                if (txtStateCustom.Tag is Panel con)
                {
                    con.Width = S(320);
                    txtStateCustom.Width = S(320) - S(10);
                    con.Invalidate();
                }
                txtStateCustom.Enabled = true;
                txtStateCustom.MaxLength = 128;
                SetPlaceholder(txtStateCustom, I18n.T("Placeholder_Custom"));
            }
            else
            {
                txtStateCustom.Enabled = false;
            }

            if (!isInitializing && currentPresence.StateHistory.TryGetValue(GetStateString(idx), out string? savedText))
            {
                txtStateCustom.Text = savedText;
            }
            else if (!isInitializing && !isProduceMode)
            {
                txtStateCustom.Text = "";
            }

            lastStateIdx = idx;
            if (!isInitializing) SaveSettings();
        }

        private void CmbBtnMode_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbBtnMode.SelectedIndex == -1) return;
            if (!isInitializing && lastBtnIdx == 3)
            {
                currentPresence.ButtonHistory["3"] = new ButtonHistoryData { L1 = txtBtn1Label.Text, U1 = txtBtn1Url.Text, L2 = txtBtn2Label.Text, U2 = txtBtn2Url.Text };
            }

            int newIdx = cmbBtnMode.SelectedIndex;

            if (newIdx == 0)
            {
                txtBtn1Label.Text = ""; txtBtn1Url.Text = ""; txtBtn2Label.Text = ""; txtBtn2Url.Text = ""; SetBtnInputsEnabled(false);
            }
            else if (newIdx == 1)
            {
                txtBtn1Label.Text = I18n.T("Button_StoreLabel_Mobile");
                txtBtn1Url.Text = "http://app.adjust.com/1ai6ouao";
                txtBtn2Label.Text = I18n.T("Button_StoreLabel_DMM");
                txtBtn2Url.Text = "https://dmg-gakuen.idolmaster-official.jp/";
                SetBtnInputsEnabled(false);
            }
            else if (newIdx == 2)
            {
                txtBtn1Label.Text = I18n.T("Button_AboutPresence");
                txtBtn1Url.Text = "https://github.com/Wea017net/GkmStatus";
                txtBtn2Label.Text = ""; txtBtn2Url.Text = ""; SetBtnInputsEnabled(false);
            }
            else if (newIdx == 3)
            {
                SetPlaceholder(txtBtn1Label, I18n.T("Placeholder_BtnLabel", 1)); SetPlaceholder(txtBtn1Url, I18n.T("Placeholder_BtnUrl", 1));
                SetPlaceholder(txtBtn2Label, I18n.T("Placeholder_BtnLabel", 2)); SetPlaceholder(txtBtn2Url, I18n.T("Placeholder_BtnUrl", 2));
                if (currentPresence.ButtonHistory.TryGetValue("3", out var h)) { txtBtn1Label.Text = h.L1; txtBtn1Url.Text = h.U1; txtBtn2Label.Text = h.L2; txtBtn2Url.Text = h.U2; }
                else { txtBtn1Label.Text = ""; txtBtn1Url.Text = ""; txtBtn2Label.Text = ""; txtBtn2Url.Text = ""; }
                SetBtnInputsEnabled(true);
                lblBtnWarning.Visible = Encoding.UTF8.GetByteCount(txtBtn1Label.Text) > 32 || Encoding.UTF8.GetByteCount(txtBtn2Label.Text) > 32;
            }
            else
            {
                lblBtnWarning.Visible = false;
            }
            lastBtnIdx = newIdx;
            if (!isInitializing) SaveSettings();
        }

        private void SaveSettings()
        {
            if (isInitializing) return;
            try
            {
                if (cmbStateType.SelectedIndex != -1 && cmbStateType.SelectedIndex != 2 && cmbStateType.SelectedIndex != 3) currentPresence.StateHistory[GetStateString(cmbStateType.SelectedIndex)] = txtStateCustom.Text;
                var config = new AppConfig { Settings = new AppSettings { StartMinimized = startMinimizedItem.Checked, ConnectOnStart = autoConnectItem.Checked, AutoCheckUpdates = checkForUpdatesItem.Checked, ShowBackgroundNotifications = notifyInBackgroundItem.Checked, NotifyOnMinimize = notifyOnMinimizeItem.Checked, MinimizeToTray = minimizeToTrayItem.Checked, AutoDetectGakumas = monitorItem.Checked, SelectedTheme = currentThemeName, SelectedLanguage = currentLanguage, LastUpdateCheck = lastUpdateCheck }, Presence = currentPresence };
                config.Presence.DetailsTypeIndex = cmbDetailsType.SelectedIndex >= 0 ? cmbDetailsType.SelectedIndex : 3;
                config.Presence.GameAppIndex = cmbGameName.SelectedIndex >= 0 ? cmbGameName.SelectedIndex : 0;
                config.Presence.StateType = GetStateString(cmbStateType.SelectedIndex);
                config.Presence.ButtonModeIndex = cmbBtnMode.SelectedIndex >= 0 ? cmbBtnMode.SelectedIndex : 0;
                config.Presence.ProducerName = txtPName.Text;
                config.Presence.ProducerLevel = (int)numPLevel.Value;
                if ((cmbStateType.SelectedIndex == 2 || cmbStateType.SelectedIndex == 3) && cmbProduceCharacter.SelectedIndex >= 0)
                {
                    config.Presence.SelectedProduceCharacterId = ProduceCharacters[cmbProduceCharacter.SelectedIndex].Id;
                }
                string json = JsonSerializer.Serialize(config, jsonOptions);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex) { Debug.WriteLine("Save Error: " + ex.Message); }
        }

        private void LoadSettings()
        {
            if (!File.Exists(configPath))
            {
                themeAuto.PerformClick();
                monitorItem.Checked = true;
                checkForUpdatesItem.Checked = true;
                notifyInBackgroundItem.Checked = true;
                notifyOnMinimizeItem.Checked = true;
                cmbStateType.SelectedIndex = 2;
                cmbBtnMode.SelectedIndex = 1;

                if (System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ja"))
                {
                    currentLanguage = "日本語";
                    I18n.CurrentLanguage = "日本語";
                    langJapanese.Checked = true;
                    langEnglish.Checked = false;
                }
                else
                {
                    currentLanguage = "English";
                    I18n.CurrentLanguage = "English";
                    langEnglish.Checked = true;
                    langJapanese.Checked = false;
                }

                if (monitorTimer != null)
                {
                    monitorTimer.Enabled = true;
                    MonitorProcess(null, EventArgs.Empty);
                }

                ApplyLanguage();
                return;
            }
            try
            {
                string json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, jsonOptions);
                if (config == null) return;
                startMinimizedItem.Checked = config.Settings.StartMinimized;
                autoConnectItem.Checked = config.Settings.ConnectOnStart;
                checkForUpdatesItem.Checked = config.Settings.AutoCheckUpdates;
                notifyInBackgroundItem.Checked = config.Settings.ShowBackgroundNotifications;
                notifyOnMinimizeItem.Checked = config.Settings.NotifyOnMinimize;
                minimizeToTrayItem.Checked = config.Settings.MinimizeToTray;
                monitorItem.Checked = config.Settings.AutoDetectGakumas;
                if (monitorTimer != null)
                {
                    monitorTimer.Enabled = monitorItem.Checked;
                    if (monitorTimer.Enabled) MonitorProcess(null, EventArgs.Empty);
                }
                currentThemeName = config.Settings.SelectedLanguage != null ? config.Settings.SelectedTheme : "自動選択";
                lastUpdateCheck = config.Settings.LastUpdateCheck;
                switch (currentThemeName) { case "ライト": themeLight.PerformClick(); break; case "ダーク": themeDark.PerformClick(); break; case "OLED": themeOLED.PerformClick(); break; default: themeAuto.PerformClick(); break; }
                currentLanguage = config.Settings.SelectedLanguage ?? (System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ja") ? "日本語" : "English");
                I18n.CurrentLanguage = currentLanguage;
                if (currentLanguage == "English") { langEnglish.Checked = true; langJapanese.Checked = false; } else { langJapanese.Checked = true; langEnglish.Checked = false; }
                currentPresence = config.Presence;

                if (string.IsNullOrEmpty(currentPresence.StateType))
                {
                    currentPresence.StateType = currentPresence.StateTypeIndex switch { 0 => "None", 1 => "PID", 2 => "Producing", 3 => "Custom", _ => "PID" };
                }

                cmbGameName.SelectedIndex = Math.Min(currentPresence.GameAppIndex, cmbGameName.Items.Count - 1);
                cmbDetailsType.SelectedIndex = currentPresence.DetailsTypeIndex;
                txtPName.Text = currentPresence.ProducerName;
                numPLevel.Value = currentPresence.ProducerLevel;
                cmbStateType.SelectedIndex = GetStateIndex(currentPresence.StateType);
                cmbCharNameLang.SelectedIndex = Math.Clamp(currentPresence.CharNameLangIndex, 0, 1);
                RefreshProduceCharacterList();

                if (currentPresence.StateHistory.TryGetValue("3", out string? oldCustom) && !currentPresence.StateHistory.ContainsKey("Custom"))
                {
                    currentPresence.StateHistory["Custom"] = oldCustom;
                }
                if (currentPresence.StateHistory.TryGetValue("1", out string? oldPid) && !currentPresence.StateHistory.ContainsKey("PID"))
                {
                    currentPresence.StateHistory["PID"] = oldPid;
                }

                if (currentPresence.StateHistory.TryGetValue(currentPresence.StateType, out string? savedState)) txtStateCustom.Text = savedState;
                cmbBtnMode.SelectedIndex = currentPresence.ButtonModeIndex;
                if (config.Settings.ConnectOnStart) InitializeRpc();
                ApplyLanguage();
            }
            catch (Exception ex) { Debug.WriteLine("Load Error: " + ex.Message); }
        }

        private static bool IsRunAtStartup()
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(REG_RUN_KEY, false);
            return key?.GetValue(APP_NAME) != null;
        }

        private static void SetRunAtStartup(bool run)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(REG_RUN_KEY, true);
                if (key != null)
                {
                    if (run) key.SetValue(APP_NAME, Application.ExecutablePath);
                    else key.DeleteValue(APP_NAME, false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("スタートアップ設定の変更に失敗しました: " + ex.Message);
            }
        }

        private static void UpdateThemeChecks(ToolStripMenuItem selected, params ToolStripMenuItem[] others) { selected.Checked = true; foreach (var o in others) o.Checked = false; }

        private void ApplyThemeAuto()
        {
            try { object? v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", null); if (v is int intVal) { if (intVal == 0) ApplyThemeDark(); else ApplyThemeLight(); return; } }
            catch { }
            SetThemeColors(defaultBackground);
        }

        private void ApplyThemeLight() => SetThemeColors(ColorTranslator.FromHtml("#f3f3f4"));
        private void ApplyThemeDark() => SetThemeColors(defaultBackground);
        private void ApplyThemeOLED() => SetThemeColors(Color.Black);

        private static IEnumerable<Control> GetAllChildControls(Control parent) { foreach (Control c in parent.Controls) { yield return c; foreach (var sub in GetAllChildControls(c)) yield return sub; } }

        private void SetThemeColors(Color bg)
        {
            this.BackColor = bg;
            bool isBright = bg.GetBrightness() > 0.5f;
            this.ForeColor = isBright ? Color.Black : Color.White;
            SetTitleBarDarkMode(!isBright);
            Color menuBg = isBright ? Color.FromArgb(235, 233, 233) : (bg.R == 0 ? Color.Black : Color.FromArgb(45, 47, 51));
            Color controlBg = isBright ? Color.White : Color.FromArgb(47, 49, 54);
            Color borderColor = isBright ? Color.FromArgb(200, 200, 200) : Color.FromArgb(70, 70, 70);

            foreach (Control c in GetAllChildControls(this))
            {
                if (c is Label lbl)
                {
                    if (lbl.Tag is string tag && tag.StartsWith("Header_")) lbl.Font = appFontBold;
                    else if (lbl == lblGameAppGuide || lbl == lblResetGuide || lbl == lblStatus || lbl == lblBtnWarning) lbl.Font = appFontMedium;
                    else lbl.Font = appFont;

                    if (lbl == lblResetGuide) lbl.ForeColor = Color.Gray;
                    else if (lbl == lblGameAppGuide) lbl.ForeColor = COLOR_PAUSE;
                    else if (lbl == lblBtnWarning || lbl == lblStatus) { /* Managed by logic */ }
                    else lbl.ForeColor = isBright ? Color.Black : Color.LightGray;
                }
                else
                {
                    c.Font = appFont;
                    if (c is MenuStrip ms)
                    {
                        ms.BackColor = menuBg;
                        ms.Font = menuFont;
                        ms.Renderer = new MyRenderer(isBright, new CustomColorTable(isBright));
                        foreach (ToolStripItem item in ms.Items)
                        {
                            if (item is ToolStripMenuItem tsmi) ApplyThemeToMenuItems(tsmi, menuBg, isBright);
                            else item.Font = menuFont;
                        }
                    }
                    else if (c is ComboBox cb) { cb.BackColor = controlBg; cb.ForeColor = isBright ? Color.Black : Color.White; }
                    else if (c is TextBox tb) { tb.BackColor = controlBg; tb.ForeColor = isBright ? Color.Black : Color.White; if (tb.Tag is Panel p) { p.BackColor = controlBg; p.Tag = borderColor; p.Invalidate(); } }
                    else if (c is NumericUpDown nu) { nu.BackColor = controlBg; nu.ForeColor = isBright ? Color.Black : Color.White; if (nu.Tag is Panel p) { p.BackColor = controlBg; p.Tag = borderColor; p.Invalidate(); } }
                    else if (c is CheckBox chk) { chk.ForeColor = isBright ? Color.Black : Color.White; }
                    else if (c is System.Windows.Forms.Button btn) { btn.Font = appFontBold; }
                }
            }

            if (trayIcon?.ContextMenuStrip != null)
            {
                var tms = trayIcon.ContextMenuStrip;
                tms.BackColor = menuBg;
                tms.Font = menuFont;
                tms.Renderer = new MyRenderer(isBright, new CustomColorTable(isBright));
                foreach (ToolStripItem item in tms.Items)
                {
                    if (item is ToolStripMenuItem tsmi) ApplyThemeToMenuItems(tsmi, menuBg, isBright);
                    else item.Font = menuFont;
                }
            }
        }

        private void ApplyThemeToMenuItems(ToolStripMenuItem item, Color menuBg, bool isBright)
        {
            item.Font = menuFont;
            if (item.DropDown != null)
            {
                item.DropDown.BackColor = menuBg;
                item.DropDown.Font = menuFont;
                item.DropDown.Renderer = new MyRenderer(isBright, new CustomColorTable(isBright));
            }
            foreach (ToolStripItem subItem in item.DropDownItems)
            {
                if (subItem is ToolStripMenuItem subMenu) ApplyThemeToMenuItems(subMenu, menuBg, isBright);
                else subItem.Font = menuFont;
            }
        }

        private void SetTitleBarDarkMode(bool dark) { try { int useDark = dark ? 1 : 0; if (DwmSetWindowAttribute(this.Handle, 20, ref useDark, sizeof(int)) != 0) DwmSetWindowAttribute(this.Handle, 19, ref useDark, sizeof(int)); } catch { } }
        private static void SetPlaceholder(IntPtr handle, string placeholderText) { SendMessage(handle, EM_SETCUEBANNER, 0, placeholderText); }
        private static void SetPlaceholder(TextBox textBox, string placeholderText) { SetPlaceholder(textBox.Handle, placeholderText); }

        private void InitializeLogic()
        {
            startTime = DateTime.UtcNow; UpdateTimestampLabel();
            monitorTimer = new System.Windows.Forms.Timer { Interval = 3000 }; monitorTimer.Tick += MonitorProcess;
            var clockTimer = new System.Windows.Forms.Timer { Interval = 1000, Enabled = true };
            clockTimer.Tick += (s, e) => { UpdateTimestampLabel(); if (client?.IsInitialized == true) client.Invoke(); };
        }

        private void InitializeRpc()
        {
            if (client?.IsInitialized == true) { UpdateUIForConnected(client.CurrentUser?.Username ?? "接続済み"); UpdateRpc(); return; }
            if (lblStatus.Text.StartsWith(I18n.T("Status_Connecting", ""))) return;
            connectionSeconds = 0;
            if (connectionTimer is null) { connectionTimer = new System.Windows.Forms.Timer { Interval = 1000 }; connectionTimer.Tick += (s, e) => { connectionSeconds++; lblStatus.Text = I18n.T("Status_Connecting", connectionSeconds); if (connectionSeconds >= CONNECTION_TIMEOUT) HandleConnectionError(I18n.T("Status_Timeout")); }; }
            lblStatus.Text = I18n.T("Status_Connecting", 0); lblStatus.ForeColor = COLOR_PAUSE; btnConnect.Enabled = false;
            UpdateTrayStatusIcon(COLOR_PAUSE, I18n.T("Tray_Status_Connecting"));
            connectionTimer.Start();
            client?.Deinitialize(); client?.Dispose(); client = null;
            string appId = GameApps[cmbGameName.SelectedIndex].AppId;
            client = new DiscordRpcClient(appId);
            client.OnReady += (sender, e) =>
    {
        this.Invoke((MethodInvoker)(() =>
        {
            connectionTimer.Stop();
            UpdateUIForConnected(e.User.Username);
            UpdateRpc();
            UpdateTrayMenuState();
            if (this.WindowState == FormWindowState.Minimized && (notifyInBackgroundItem?.Checked == true))
            {
                trayIcon.Tag = null;
                trayIcon.ShowBalloonTip(3000, I18n.T("App_Name"), I18n.T("Status_Connected_Notify", e.User.Username), ToolTipIcon.Info);
            }
        }));
    };
            client.OnError += (sender, e) => { this.Invoke((MethodInvoker)(() => { HandleConnectionError(I18n.T("Status_Error", e.Message)); })); };
            try { if (!client.Initialize()) HandleConnectionError(I18n.T("Status_InitFailed")); } catch (Exception ex) { HandleConnectionError(I18n.T("Status_Exception", ex.Message)); }
        }

        private void HandleConnectionError(string message)
        {
            connectionTimer?.Stop();
            lblStatus.Text = message; lblStatus.ForeColor = COLOR_ERROR;
            statusToolTip.SetToolTip(lblStatus, message);
            AdjustStatusVerticalPosition();
            btnConnect.Text = I18n.T("Button_Connect"); btnConnect.BackColor = COLOR_CONNECT; btnConnect.Enabled = true;
            btnUpdate.Enabled = false; btnUpdate.BackColor = COLOR_DISABLED;
            btnDisconnect.Enabled = false; btnDisconnect.BackColor = COLOR_DISABLED;
            UpdateTrayStatusIcon(null);
            UpdateTrayMenuState();
            client?.Dispose(); client = null;
        }

        private void PauseRpc()
        {
            if (client?.IsInitialized == true) client.ClearPresence();
            UpdateUIForPause();
            if (this.WindowState == FormWindowState.Minimized && (notifyInBackgroundItem?.Checked == true))
            {
                trayIcon.Tag = null;
                trayIcon.ShowBalloonTip(3000, I18n.T("App_Name"), I18n.T("Status_Disconnected_Notify"), ToolTipIcon.Info);
            }
        }
        private void DisposeRpc()
        {
            connectionTimer?.Stop();
            if (client is not null) { client.ClearPresence(); client.Deinitialize(); client.Dispose(); client = null; }
            UpdateUIForDisconnect();
            if (this.WindowState == FormWindowState.Minimized && (notifyInBackgroundItem?.Checked == true))
            {
                trayIcon.Tag = null;
                trayIcon.ShowBalloonTip(3000, I18n.T("App_Name"), I18n.T("Status_ManualDisconnected_Notify"), ToolTipIcon.Info);
            }
        }

        private void UpdateUIForConnected(string username)
        {
            string status = I18n.T("Status_Connected", username);
            lblStatus.Text = status;
            lblStatus.ForeColor = COLOR_CONNECT;
            statusToolTip.SetToolTip(lblStatus, status.Replace("\n", " "));
            AdjustStatusVerticalPosition();
            btnConnect.Text = I18n.T("Button_Pause");
            btnConnect.BackColor = COLOR_PAUSE;
            btnConnect.Enabled = true;
            btnUpdate.Enabled = true;
            btnUpdate.BackColor = COLOR_PRIMARY;
            btnDisconnect.Enabled = true;
            btnDisconnect.BackColor = COLOR_ERROR;
            btnResetTime.Enabled = true;
            btnResetTime.BackColor = COLOR_PRIMARY;
            UpdateTrayStatusIcon(COLOR_CONNECT, I18n.T("Tray_Status_Connected"));
            UpdateTrayMenuState();
        }
        private void UpdateUIForPause()
        {
            string status = I18n.T("Status_Paused");
            lblStatus.Text = status;
            lblStatus.ForeColor = COLOR_PAUSE;
            statusToolTip.SetToolTip(lblStatus, status.Replace("\n", " "));
            AdjustStatusVerticalPosition();
            btnConnect.Text = I18n.T("Button_Connect");
            btnConnect.BackColor = COLOR_CONNECT;
            btnConnect.Enabled = true;
            btnUpdate.Enabled = false;
            btnUpdate.BackColor = COLOR_DISABLED;
            btnDisconnect.Enabled = true;
            btnDisconnect.BackColor = COLOR_ERROR;
            btnResetTime.Enabled = true;
            btnResetTime.BackColor = COLOR_PRIMARY;
            UpdateTrayStatusIcon(COLOR_PAUSE, I18n.T("Tray_Status_Paused"));
            UpdateTrayMenuState();
        }
        private void UpdateUIForDisconnect()
        {
            connectionTimer?.Stop();
            string status = I18n.T("Status_Disconnected");
            lblStatus.Text = status;
            lblStatus.ForeColor = Color.Gray;
            statusToolTip.SetToolTip(lblStatus, status);
            AdjustStatusVerticalPosition();
            btnConnect.Text = I18n.T("Button_Connect");
            btnConnect.BackColor = COLOR_CONNECT;
            btnConnect.Enabled = true;
            btnUpdate.Enabled = false;
            btnUpdate.BackColor = COLOR_DISABLED;
            btnDisconnect.Enabled = false;
            btnDisconnect.BackColor = COLOR_DISABLED;
            btnResetTime.Enabled = false;
            btnResetTime.BackColor = COLOR_DISABLED;
            UpdateTrayStatusIcon(null);
            UpdateTrayMenuState();
        }
        private void UpdateRpc()
        {
            if (client?.IsInitialized != true) return;
            string SafeTrim(string text) => SafeTrimUtf8(text, 128);
            bool IsValidUrl(string url) => !string.IsNullOrEmpty(url) &&
                                          (url.StartsWith("http://") || url.StartsWith("https://")) &&
                                          !JapaneseRegex().IsMatch(url);

            string? pName = string.IsNullOrWhiteSpace(txtPName.Text) ? I18n.T("Placeholder_PName") : txtPName.Text;
            string? details = null;
            switch (cmbDetailsType.SelectedIndex) { case 1: details = $"{pName}"; break; case 2: details = $"PLv{numPLevel.Value}"; break; case 3: details = $"{pName} | PLv{numPLevel.Value}"; break; }
            string? state = null;

            if (cmbStateType.SelectedIndex == 1)
            {
                state = $"P-ID: {(string.IsNullOrWhiteSpace(txtStateCustom.Text) ? I18n.T("State_NotSet") : txtStateCustom.Text)}";
            }
            else if (cmbStateType.SelectedIndex == 2 || cmbStateType.SelectedIndex == 3)
            {
                var pc = ProduceCharacters.Find(c => c.Id == currentPresence.SelectedProduceCharacterId);
                if (pc != null)
                {
                    string name = currentPresence.CharNameLangIndex == 1 ? pc.NameEn : pc.Display;
                    if (cmbStateType.SelectedIndex == 3)
                    {
                        state = I18n.T("State_Producing_Format", name);
                    }
                    else
                    {
                        state = I18n.T("State_Idol_Format", name);
                    }
                }
            }
            else if (cmbStateType.SelectedIndex == 4)
            {
                state = string.IsNullOrWhiteSpace(txtStateCustom.Text) ? "" : txtStateCustom.Text;
            }
            Button[]? buttons = null;
            if (cmbBtnMode.SelectedIndex != 0)
            {
                string SafeTrimBtn(string t) => SafeTrimUtf8(t, 32);

                var b1 = (!string.IsNullOrEmpty(txtBtn1Label.Text) && IsValidUrl(txtBtn1Url.Text)) ? new Button { Label = SafeTrimBtn(txtBtn1Label.Text), Url = txtBtn1Url.Text } : null;
                var b2 = (!string.IsNullOrEmpty(txtBtn2Label.Text) && IsValidUrl(txtBtn2Url.Text)) ? new Button { Label = SafeTrimBtn(txtBtn2Label.Text), Url = txtBtn2Url.Text } : null;
                if (b1 != null && b2 != null) buttons = [b1, b2]; else if (b1 != null) buttons = [b1]; else if (b2 != null) buttons = [b2];
            }
            if (!string.IsNullOrEmpty(details) && details.Length < 2) details = "";
            if (!string.IsNullOrEmpty(state) && state.Length < 2) state = "";

            string gameName = GameApps[cmbGameName.SelectedIndex].Name;
            try { client.SetPresence(new RichPresence { Details = SafeTrim(details ?? ""), State = SafeTrim(state ?? ""), Assets = new Assets { LargeImageKey = "app", LargeImageText = SafeTrimUtf8($"{I18n.T("App_Name")} v{Application.ProductVersion}", 128) }, Buttons = buttons, Timestamps = new Timestamps(startTime) }); }
            catch (Exception ex) { Debug.WriteLine("RPC Update Error: " + ex.Message); }
        }

        private void MonitorProcess(object? sender, EventArgs e) { var processes = Process.GetProcessesByName(PROCESS_NAME); bool isRunningNow = processes.Length > 0; if (isRunningNow && !wasProcessRunning) { startTime = DateTime.UtcNow; UpdateTimestampLabel(); InitializeRpc(); } else if (!isRunningNow && wasProcessRunning) { PauseRpc(); } wasProcessRunning = isRunningNow; }
        private void UpdateDetailsInputs(object? sender, EventArgs e)
        {
            int idx = cmbDetailsType.SelectedIndex;
            txtPName.Enabled = (idx == 1 || idx == 3);
            numPLevel.Enabled = (idx == 2 || idx == 3);
            if (!isInitializing) SaveSettings();
        }
        private void SetBtnInputsEnabled(bool e) { txtBtn1Label.Enabled = e; txtBtn1Url.Enabled = e; txtBtn2Label.Enabled = e; txtBtn2Url.Enabled = e; }
        private void UpdateTimestampLabel()
        {
            bool isSessionActive = client?.IsInitialized == true;
            TimeSpan ts = isSessionActive ? DateTime.UtcNow - startTime : TimeSpan.Zero;

            lblStartTime.Text = $"{I18n.T("Timestamp_Label")}: {ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            lblStartTime.ForeColor = this.BackColor.GetBrightness() > 0.5f ? Color.Black : Color.LightGray;
        }

        private TextBox CreateText(Point loc, int w)
        {
            int autoHeight;
            using (var temp = new ComboBox { Font = appFont, FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDownList })
            {
                autoHeight = temp.PreferredHeight;
            }
            Panel container = new() { Location = loc, Size = new(w, autoHeight), BackColor = Color.FromArgb(47, 49, 54), Parent = this };
            container.Paint += (s, e) => { if (container.Tag is Color col) ControlPaint.DrawBorder(e.Graphics, container.ClientRectangle, col, ButtonBorderStyle.Solid); };
            TextBox tb = new() { BorderStyle = BorderStyle.None, BackColor = container.BackColor, ForeColor = Color.White, Font = appFont, Location = new(S(5), (autoHeight - TextRenderer.MeasureText("Ag", appFont).Height) / 2 - S(1)), Width = w - S(10) };
            container.Controls.Add(tb);
            tb.Tag = container; return tb;
        }

        private ComboBox CreateCombo(Point loc, int w, string[] i)
        {
            var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(47, 49, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Parent = this, Font = appFont, Location = loc, Width = w };
            cb.Items.AddRange(i); return cb;
        }

        private NumericUpDown CreateNumeric(Point loc, int w)
        {
            int autoHeight;
            using (var temp = new ComboBox { Font = appFont, FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDownList })
            {
                autoHeight = temp.PreferredHeight;
            }
            Panel container = new() { Location = loc, Size = new(w, autoHeight), BackColor = Color.FromArgb(47, 49, 54), Parent = this };
            container.Paint += (s, e) => { if (container.Tag is Color col) ControlPaint.DrawBorder(e.Graphics, container.ClientRectangle, col, ButtonBorderStyle.Solid); };
            NumericUpDown nud = new() { BorderStyle = BorderStyle.None, BackColor = container.BackColor, ForeColor = Color.White, Font = appFont, Location = new(S(5), (autoHeight - TextRenderer.MeasureText("Ag", appFont).Height) / 2 - S(1)), Width = w - S(10), Minimum = 1, Maximum = 100 };
            container.Controls.Add(nud);
            nud.Tag = container; return nud;
        }

        private void AdjustStatusVerticalPosition()
        {
            if (lblStatus == null || btnConnect == null) return;
            int btnHeight = btnConnect.Height;
            int areaCenterY = (footerButtonsY + S(20)) + (btnHeight / 2);
            lblStatus.Top = areaCenterY - (lblStatus.Height / 2);
        }

        private void RefreshProduceCharacterList()
        {
            var currentId = currentPresence.SelectedProduceCharacterId;
            cmbProduceCharacter.Items.Clear();
            bool isEn = currentPresence.CharNameLangIndex == 1;

            foreach (var c in ProduceCharacters)
            {
                cmbProduceCharacter.Items.Add(isEn ? c.NameEn : c.Display);
            }

            int idx = ProduceCharacters.FindIndex(c => c.Id == currentId);
            cmbProduceCharacter.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private System.Windows.Forms.Button CreateButton(string t, Point l, int w, Color bc)
        {
            int btnHeight = Math.Max(S(35), TextRenderer.MeasureText("Ag", appFontBold).Height + S(15));
            return new System.Windows.Forms.Button { Text = t, Location = l, Size = new Size(w, btnHeight), BackColor = bc, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, Font = appFontBold, Parent = this, Cursor = Cursors.Hand };
        }

        private static string SafeTrimUtf8(string text, int maxBytes = 31)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var enc = Encoding.UTF8;
            if (enc.GetByteCount(text) <= maxBytes) return text;
            int lo = 0, hi = text.Length;
            while (lo < hi) { int mid = (lo + hi + 1) / 2; if (enc.GetByteCount(text[..mid]) <= maxBytes) lo = mid; else hi = mid - 1; }
            return text[..lo];
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]")]
        private static partial System.Text.RegularExpressions.Regex JapaneseRegex();

        private void UpdateTrayStatusIcon(Color? color, string? statusText = null)
        {
            if (trayIcon == null) return;

            var baseIcon = this.Icon ?? SystemIcons.Application;
            string appDisplayName = I18n.T("App_Name");
            trayIcon.Text = string.IsNullOrEmpty(statusText) ? appDisplayName : $"{appDisplayName} - {statusText}";

            if (color == null)
            {
                trayIcon.Icon = baseIcon;
                currentTrayIconStatus?.Dispose();
                currentTrayIconStatus = null;
                return;
            }

            try
            {
                using var bitmap = baseIcon.ToBitmap();
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    int size = bitmap.Width / 4;
                    int x = bitmap.Width - size - 2;
                    int y = 2;

                    using var brush = new SolidBrush(color.Value);
                    g.FillEllipse(brush, x, y, size, size);

                    using var pen = new Pen(Color.White, 1);
                    g.DrawEllipse(pen, x, y, size, size);
                }

                var newIcon = Icon.FromHandle(bitmap.GetHicon());
                trayIcon.Icon = newIcon;

                currentTrayIconStatus?.Dispose();
                currentTrayIconStatus = newIcon;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Tray Icon Status Draw Error: " + ex.Message);
            }
        }

        private void UpdateTrayMenuState()
        {
            if (trayIcon.ContextMenuStrip == null) return;

            bool isConnected = client?.IsInitialized == true;
            bool isPaused = isConnected && (btnConnect.Text == I18n.T("Button_Connect"));

            trayMenuConnect.Visible = !isConnected || isPaused;
            trayMenuPause.Visible = isConnected && !isPaused;
            trayMenuDisconnect.Visible = isConnected;
        }
    }
}