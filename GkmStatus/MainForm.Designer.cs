using GkmStatus.src;
using GkmStatus.src.native;
using GkmStatus.src.ui;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using static GkmStatus.src.AppConstants;
using static GkmStatus.src.Characters;
using static GkmStatus.src.ui.I18n.Text_List;
using static GkmStatus.src.AppSettingsHelper;
using static GkmStatus.src.StartupManager;

namespace GkmStatus
{
    partial class MainForm
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

        //GUIDesignerをだます
        private void InitializeComponent()
        {

        }

        void Cfg<T>(T c, Action<T> action) => action?.Invoke(c);
        private int S(int value) => (int)Math.Round(value * uiScale);

        private int StanderdCmbHight()
        {
            //後でキャッシュする
            using var temp = new ComboBox { Font = _fontManager.AppFont, FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDownList };
            return temp.PreferredHeight;
        }

        private T WrapStylePanel<T>(T control, Point loc, int w) where T : Control
        {
            int autoHeight = StanderdCmbHight();

            Panel container = new()
            {
                Location = loc,
                Size = new(w, autoHeight),
                BackColor = Default_BackColor,
                Parent = this
            };

            container.Paint += (s, e) =>
            {
                if (container.Tag is Color col)
                    ControlPaint.DrawBorder(e.Graphics, container.ClientRectangle, col, ButtonBorderStyle.Solid);
            };

            control.Location = new Point(S(5), (autoHeight - TextRenderer.MeasureText("Ag", control.Font).Height) / 2 - S(1));
            control.Width = w - S(10);
            control.Parent = container;

            control.Tag = container;

            return control;
        }

        private Button CreateButton(Point l, int w, Color bc)
        {
            int btnHeight = Math.Max(S(35), TextRenderer.MeasureText("Ag", _fontManager.AppFontBold).Height + S(15));
            return new Button
            {
                Location = l,
                Size = new Size(w, btnHeight),
                BackColor = bc,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = _fontManager.AppFontBold,
                Cursor = Cursors.Hand,
                Parent = this
            };
        }

        private ComboBox CreateCombo(Point loc, int w, string[] i)
        {
            var cb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Default_BackColor,
                ForeColor = Default_ForeColor,
                FlatStyle = FlatStyle.Flat,
                Font = _fontManager.AppFont,
                Location = loc,
                Width = w,
                Parent = this
            };
            cb.Items.AddRange(i);
            return cb;
        }

        private NumericUpDown CreateNumeric(Point loc, int w)
        {
            var nud = new NumericUpDown
            {
                BorderStyle = BorderStyle.None,
                BackColor = Default_BackColor,
                ForeColor = Default_ForeColor,
                Font = _fontManager.AppFont,
                Minimum = 1,
                Maximum = 100
            };
            return WrapStylePanel(nud, loc, w);
        }

        private TextBox CreateText(Point loc, int w)
        {
            var tb = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Default_BackColor,
                ForeColor = Default_ForeColor,
                Font = _fontManager.AppFont
            };
            return WrapStylePanel(tb, loc, w);
        }


        private void ApplyComponent()
        {
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;

            this.ClientSize = new Size(S(520), S(630));
            this.FormBorderStyle = FormBorderStyle.FixedSingle;

            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(32, 34, 37);
            this.ForeColor = Color.White;
            this.Font = _fontManager.AppFont;

            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using Stream stream = assembly.GetManifestResourceStream("GkmStatus.Resources.app.ico");
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
            trayMenu.Opening += (s, e) =>
            {
                _trayIconManager.UpdateMenuState(rpc.Status);
                Native.SetForegroundWindow(this.Handle);
                string stateStr = GetStateString(cmbStateType.SelectedIndex);
                _trayIconManager.SetProduceMenuEnabled(stateStr == "Idol" || stateStr == "Producing");
            };
            trayMenu.Items.Add(I18n.T(Tray_Open), null, (s, e) => RestoreFromTray());
            trayMenu.Items.Add(new ToolStripSeparator());

            trayMenuDetails = new ToolStripMenuItem(I18n.T(Header_Details)) { 
                Name = "trayMenuDetails"
            };
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
                            trayIcon.ShowBalloonTip(3000, I18n.T(App_Name), I18n.T(Notify_TrayDetailsChanged), ToolTipIcon.Info);
                        }
                    };
                    trayMenuDetails.DropDownItems.Add(item);
                }
            };

            trayMenuState = new ToolStripMenuItem(I18n.T(Header_State))
            {
                Name = "trayMenuState"
            };
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
                        if (trayMenuProduce != null)
                        {
                            string stateStr = GetStateString(index);
                            trayMenuProduce.Enabled = (stateStr == "Idol" || stateStr == "Producing");
                        }

                        if (notifyInBackgroundItem?.Checked == true)
                        {
                            trayIcon.Tag = null;
                            trayIcon.ShowBalloonTip(3000, I18n.T(App_Name), I18n.T(Notify_TrayStateChanged), ToolTipIcon.Info);
                        }
                    };
                    trayMenuState.DropDownItems.Add(item);
                }
            };

            trayMenuProduce = new ToolStripMenuItem(I18n.T(Tray_ProducingIdol))
            {
                Name = "trayMenuProduce"
            };
            trayMenuProduce.DropDownItems.Add(new ToolStripMenuItem("..."));
            trayMenuProduce.DropDownOpening += (s, e) =>
            {
                trayMenuProduce.DropDownItems.Clear();
                string currentId = (GetStateString(cmbStateType.SelectedIndex) == "Idol") ? CurrentPresence.SelectedIdolCharacterId : CurrentPresence.SelectedProduceCharacterId;

                for (int i = 0; i < ProduceCharacters.Count; i++)
                {
                    var pc = ProduceCharacters[i];
                    string displayName = CurrentPresence.CharNameLangIndex == 1 ? pc.NameEn : pc.Display;
                    var item = new ToolStripMenuItem(displayName);
                    if (currentId == pc.Id) item.Checked = true;

                    int index = i;
                    item.Click += (sender, ev) =>
                    {
                        if (index >= 0 && index < cmbProduceCharacter.Items.Count)
                        {
                            cmbProduceCharacter.SelectedIndex = index;
                        }
                        UpdateRpc();

                        if (notifyInBackgroundItem?.Checked == true)
                        {
                            trayIcon.Tag = null;
                            trayIcon.ShowBalloonTip(3000, I18n.T(App_Name), I18n.T(Notify_TrayIdolChanged), ToolTipIcon.Info);
                        }
                    };

                    trayMenuProduce.DropDownItems.Add(item);
                }
            };

            trayMenu.Items.Add(trayMenuDetails);
            trayMenu.Items.Add(trayMenuState);
            trayMenu.Items.Add(trayMenuProduce);

            var traySepSettings = new ToolStripSeparator { Tag = "SepSettings" };
            trayMenu.Items.Add(traySepSettings);

            trayMenuConnect = new ToolStripMenuItem(I18n.T(Button_Connect), null, (s, e) => InitializeRpc())
            {
                Name = "trayMenuConnect"
            };
            trayMenuPause = new ToolStripMenuItem(I18n.T(Button_Pause), null, (s, e) => PauseRpc())
            {
                Name = "trayMenuPause"
            };
            trayMenuDisconnect = new ToolStripMenuItem(I18n.T(Button_Disconnect), null, (s, e) => DisposeRpc())
            {
                Name = "trayMenuDisconnect"
            };

            trayMenu.Items.AddRange([trayMenuConnect, trayMenuPause, trayMenuDisconnect]);

            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(I18n.T(Tray_Exit), null, (s, e) => Application.Exit());

            trayIcon.ContextMenuStrip = trayMenu;

            int y = S(40);
            void AddHeader(int top, string tag)
            {
                var lbl = new Label {Location = new Point(S(20), top), AutoSize = true, Font = _fontManager.AppFontBold, ForeColor = Color.LightGray, Tag = tag };
                this.Controls.Add(lbl);
            }

            AddHeader(y, "Header_GameName"); y += S(25);
            cmbGameName = CreateCombo(new(S(20), y), S(210), [.. GameApps.Select(g => g.Name)]);
            cmbGameName.SelectedIndex = 0;
            cmbGameName.SelectedIndexChanged += CmbGameName_SelectedIndexChanged;

            lblGameAppGuide = new Label
            {
                Location = new Point(S(240), y - S(7)),
                AutoSize = true,
                Visible = false,
                ForeColor = COLOR_PAUSE,
                Font = _fontManager.AppFontMedium
            };
            this.Controls.Add(lblGameAppGuide);

            gameAppGuideTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            gameAppGuideTimer.Tick += (s, e) =>
            {
                lblGameAppGuide.Visible = false;
                gameAppGuideTimer.Stop();
            };

            y += S(45);

            AddHeader(y, "Header_Details"); y += S(25);
            cmbDetailsType = CreateCombo(new Point(S(20), y), S(180), [I18n.T(Details_None), I18n.T(Details_PName), I18n.T(Details_PLv), I18n.T(Details_Both)]);
            cmbDetailsType.SelectedIndex = 3; cmbDetailsType.SelectedIndexChanged += UpdateDetailsInputs;
            txtPName = CreateText(new Point(S(210), y), S(200));
            Native.SetPlaceholder(txtPName, I18n.T(Placeholder_PName));
            txtPName.MaxLength = 10;
            txtPName.TextChanged += (s, e) => { if (!isInitializing) SaveSettings(); };
            numPLevel = CreateNumeric(new Point(S(420), y), S(80));
            numPLevel.ValueChanged += (s, e) => { if (!isInitializing) SaveSettings(); };
            y += S(45);

            AddHeader(y, "Header_State"); y += S(25);

            cmbStateType = CreateCombo(
                new(S(20), y),
                S(150),
                [I18n.T(State_None), I18n.T(State_PID), I18n.T(State_Idol), I18n.T(State_Producing), I18n.T(State_Custom)]
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
                [I18n.T(CharName_JP), I18n.T(CharName_EN)]
            );
            cmbCharNameLang.Visible = false;
            cmbCharNameLang.SelectedIndex = 0;
            cmbCharNameLang.SelectedIndexChanged += (s, e) =>
            {
                if (!isInitializing)
                {
                    CurrentPresence.CharNameLangIndex = cmbCharNameLang.SelectedIndex;
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
                    string characterId = ProduceCharacters[cmbProduceCharacter.SelectedIndex].Id;
                    string stateStr = GetStateString(cmbStateType.SelectedIndex);
                    if (stateStr == "Idol") CurrentPresence.SelectedIdolCharacterId = characterId;
                    else if (stateStr == "Producing") CurrentPresence.SelectedProduceCharacterId = characterId;

                    if (!isInitializing) SaveSettings();
                }
            };

            txtStateCustom = CreateText(new Point(S(180), y), S(320)); txtStateCustom.Enabled = false;
            cmbStateType.SelectedIndexChanged += CmbStateType_SelectedIndexChanged;
            y += S(45);

            AddHeader(y, "Header_Timestamp"); y += S(25);
            lblStartTime = new Label { Text = I18n.T(Timestamp_Label) + ": 00:00:00", Location = new Point(S(20), y + S(7)), AutoSize = true };
            this.Controls.Add(lblStartTime);
            btnResetTime = CreateButton(new Point(S(170), y), S(120), COLOR_PRIMARY);
            btnResetTime.Click += (s, e) =>
            {
                startTime = DateTime.UtcNow;
                UpdateTimestampLabel();
                lblResetGuide.Visible = true;
                resetGuideTimer.Stop();
                resetGuideTimer.Start();
                if (rpc.IsInitialized == true) UpdateRpc();
            };
            y += S(45);

            lblResetGuide = new Label
            {
                Text = I18n.T(Timestamp_Guide),
                Location = new Point(S(300), y - S(38)),
                AutoSize = true,
                Visible = false,
                ForeColor = Color.Gray,
                Font = _fontManager.AppFontMedium
            };
            this.Controls.Add(lblResetGuide);

            resetGuideTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            resetGuideTimer.Tick += (s, e) =>
            {
                lblResetGuide.Visible = false;
                resetGuideTimer.Stop();
            };

            AddHeader(y, "Header_Buttons"); y += S(25);
            cmbBtnMode = CreateCombo(new Point(S(20), y), S(150), [I18n.T(Button_ModeNone), I18n.T(Button_ModeStore), I18n.T(Button_ModeApp), I18n.T(Button_ModeCustom)]);
            cmbBtnMode.SelectedIndexChanged += CmbBtnMode_SelectedIndexChanged;

            lblBtnModeNote = new Label
            {
                Location = new Point(S(180), y + S(3)),
                AutoSize = true,
                Visible = false,
                ForeColor = Color.Gray,
                Font = _fontManager.AppFontMedium
            };
            this.Controls.Add(lblBtnModeNote);

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
                Font = _fontManager.AppFontMedium
            };
            this.Controls.Add(lblBtnWarning);

            void CheckBtnWarning(object sender, EventArgs e)
            {
                bool labelOver = Encoding.UTF8.GetByteCount(txtBtn1Label.Text) > 32 || Encoding.UTF8.GetByteCount(txtBtn2Label.Text) > 32;
                bool urlJapanese = JapaneseRegex().IsMatch(txtBtn1Url.Text + txtBtn2Url.Text);

                var sb = new StringBuilder();
                if (labelOver) sb.AppendLine(I18n.T(Button_Warning_LabelLength));
                if (urlJapanese) sb.Append(I18n.T(Button_Warning_UrlJp));

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

            btnConnect = CreateButton(new Point(S(20), y), S(100), COLOR_CONNECT);
            btnConnect.Click += (s, e) =>
            {
                if (rpc.Status == RpcStatus.Connected && !isManualPaused)
                    PauseRpc();
                else
                    InitializeRpc();
            };
            btnUpdate = CreateButton(new Point(S(130), y), S(100), COLOR_PRIMARY);
            btnUpdate.Enabled = false; btnUpdate.Click += async (s, e) =>
            {
                UpdateRpc();
                string originalText = lblStatus.Text;
                Color originalColor = lblStatus.ForeColor;

                string updatedText = I18n.T(Status_Updated);
                lblStatus.Text = updatedText;
                lblStatus.ForeColor = COLOR_PRIMARY;
                statusToolTip.SetToolTip(lblStatus, updatedText.Replace("\n", " "));
                AdjustStatusVerticalPosition();

                await System.Threading.Tasks.Task.Delay(2000);

                if (this.IsDisposed) return;

                if (rpc.Status == RpcStatus.Connected && !isManualPaused)
                {
                    lblStatus.ForeColor = originalColor;
                    lblStatus.Text = originalText;
                    statusToolTip.SetToolTip(lblStatus, originalText.Replace("\n", " "));
                    AdjustStatusVerticalPosition();
                }
                else if (rpc.Status == RpcStatus.Paused || isManualPaused)
                {
                    UpdateUIForPause();
                }
                else
                {
                    UpdateUIForDisconnect();
                }
            };
            btnDisconnect = CreateButton(new Point(S(240), y), S(100), COLOR_ERROR);
            btnDisconnect.Enabled = false; btnDisconnect.Click += (s, e) => DisposeRpc();

            lblStatus = new Label { Text = I18n.T(Status_Disconnected), Location = new Point(S(352), y), AutoSize = true, ForeColor = Color.Gray, Font = _fontManager.AppFontMedium };
            statusToolTip = new ToolTip();
            statusToolTip.SetToolTip(lblStatus, lblStatus.Text);
            this.Controls.Add(btnConnect); this.Controls.Add(btnUpdate); this.Controls.Add(btnDisconnect); this.Controls.Add(lblStatus);


            AdjustStatusVerticalPosition();

            var menu = new MenuStrip();
            fileMenu = new(I18n.T(Menu_File));
            exitItem = new(I18n.T(Menu_Exit)) { ShortcutKeyDisplayString = "Alt+F4" };
            exitItem.Click += (s, e) => { Application.Exit(); };
            fileMenu.DropDownItems.Add(exitItem);

            settingsMenu = new(I18n.T(Menu_Settings));
            runAtStartupItem = new(I18n.T(Menu_RunAtStartup)) { CheckOnClick = true, Checked = IsRunAtStartup() };
            runAtStartupItem.CheckedChanged += (s, e) => SetRunAtStartup(runAtStartupItem.Checked);

            startMinimizedItem = new(I18n.T(Menu_StartMinimized)) { CheckOnClick = true };
            startMinimizedItem.CheckedChanged += (s, e) => SaveSettings();

            autoConnectItem = new(I18n.T(Menu_AutoConnect)) { CheckOnClick = true };
            autoConnectItem.CheckedChanged += (s, e) => SaveSettings();

            checkForUpdatesItem = new(I18n.T(Menu_CheckForUpdates)) { CheckOnClick = true, Checked = true };
            checkForUpdatesItem.CheckedChanged += (s, e) => SaveSettings();

            notifyInBackgroundItem = new(I18n.T(Menu_NotifyInBackground)) { CheckOnClick = true, Checked = true };
            notifyInBackgroundItem.CheckedChanged += (s, e) => SaveSettings();

            notifyOnMinimizeItem = new(I18n.T(Menu_NotifyOnMinimize)) { CheckOnClick = true, Checked = true };
            notifyOnMinimizeItem.CheckedChanged += (s, e) => SaveSettings();

            minimizeToTrayItem = new(I18n.T(Menu_MinimizeToTray)) { CheckOnClick = true, Checked = true };
            minimizeToTrayItem.CheckedChanged += (s, e) => SaveSettings();

            monitorItem = new(I18n.T(Menu_MonitorProcess)) { CheckOnClick = true, Checked = true };
            monitorItem.CheckedChanged += (s, e) => {
                _processWatcher.Enabled = monitorItem.Checked;
                if (_processWatcher.Enabled) _processWatcher.ForceCheck();

                if (rpc.IsInitialized != true) UpdateUIForDisconnect();
                SaveSettings();
            };

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

            settingsMenu.DropDown.Closing += (s, e) =>
            {
                if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true;
            };

            viewMenu = new ToolStripMenuItem(I18n.T(Menu_View));
            themeMenu = new ToolStripMenuItem(I18n.T(Menu_Theme));
            themeAuto = new ToolStripMenuItem(I18n.T(Menu_ThemeAuto));
            themeLight = new ToolStripMenuItem(I18n.T(Menu_ThemeLight));
            themeDark = new ToolStripMenuItem(I18n.T(Menu_ThemeDark));
            themeOLED = new ToolStripMenuItem(I18n.T(Menu_ThemeOLED));

            themeAuto.Click += (s, e) => { _themeManager.ApplyTheme(this, AppTheme.Auto); ThemeManager.UpdateThemeChecks(themeAuto, themeLight, themeDark, themeOLED); SaveSettings(); };
            themeLight.Click += (s, e) => { _themeManager.ApplyTheme(this, AppTheme.Light); ThemeManager.UpdateThemeChecks(themeLight, themeAuto, themeDark, themeOLED); SaveSettings(); };
            themeDark.Click += (s, e) => { _themeManager.ApplyTheme(this, AppTheme.Dark); ThemeManager.UpdateThemeChecks(themeDark, themeAuto, themeLight, themeOLED); SaveSettings(); };
            themeOLED.Click += (s, e) => { _themeManager.ApplyTheme(this, AppTheme.OLED); ThemeManager.UpdateThemeChecks(themeOLED, themeAuto, themeLight, themeDark); SaveSettings(); };

            themeMenu.DropDownItems.AddRange([themeAuto, themeLight, themeDark, themeOLED]);
            themeMenu.DropDown.Closing += (s, e) =>
            {
                if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true;
            };
            viewMenu.DropDownItems.Add(themeMenu);

            var separator2 = new ToolStripSeparator();
            langMenu = new ToolStripMenuItem(I18n.T(Menu_Language));
            langEnglish = new ToolStripMenuItem("English");
            langJapanese = new ToolStripMenuItem("日本語");

            langEnglish.Click += (s, e) => { I18n.CurrentLanguage = I18n.Language.English; ThemeManager.UpdateThemeChecks(langEnglish, langJapanese); ApplyLanguage(); SaveSettings(); };
            langJapanese.Click += (s, e) => { I18n.CurrentLanguage = I18n.Language.Japanese; ThemeManager.UpdateThemeChecks(langJapanese, langEnglish); ApplyLanguage(); SaveSettings(); };

            langMenu.DropDownItems.AddRange([langEnglish, langJapanese]);
            langMenu.DropDown.Closing += (s, e) =>
            {
                if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true;
            };
            viewMenu.DropDownItems.Add(separator2);
            viewMenu.DropDownItems.Add(langMenu);

            helpMenu = new ToolStripMenuItem(I18n.T(Menu_Help));
            openAppLocationMenu = new ToolStripMenuItem(I18n.T(Menu_OpenAppLocation));
            openConfigLocationMenu = new ToolStripMenuItem(I18n.T(Menu_OpenConfigLocation));

            openAppLocationMenu.Click += (s, e) =>
            {
                try { Process.Start("explorer.exe", $"/select,\"{Process.GetCurrentProcess().MainModule?.FileName}\""); }
                catch { MessageBox.Show(I18n.T(Error_Browser)); }
            };

            openConfigLocationMenu.Click += (s, e) =>
            {
                try
                {
                    if (File.Exists(CONFIG_PATH)) Process.Start("explorer.exe", $"/select,\"{CONFIG_PATH}\"");
                    else Process.Start("explorer.exe", $"\"{Path.GetDirectoryName(CONFIG_PATH)}\"");
                }
                catch { MessageBox.Show(I18n.T(Error_Browser)); }
            };

            githubMenu = new ToolStripMenuItem(I18n.T(Menu_Github));
            githubMenu.Click += (s, e) => { try { Process.Start(new ProcessStartInfo("https://github.com/Wea017net/GkmStatus") { UseShellExecute = true }); } catch { MessageBox.Show(I18n.T(Error_Browser)); } };

            checkUpdateMenuItem = new ToolStripMenuItem(I18n.T(Menu_CheckUpdateNow));
            checkUpdateMenuItem.Click += (s, e) => _ = CheckForUpdatesAsync(manual: true);

            aboutMenu = new ToolStripMenuItem(I18n.T(Menu_About));
            aboutMenu.Click += (s, e) => ShowAboutDialog();

            var separatorHelp = new ToolStripSeparator();
            helpMenu.DropDownItems.AddRange([openAppLocationMenu, openConfigLocationMenu, separatorHelp, githubMenu, checkUpdateMenuItem, aboutMenu]);

            menu.Items.Add(fileMenu); menu.Items.Add(settingsMenu); menu.Items.Add(viewMenu); menu.Items.Add(helpMenu);
            this.Controls.Add(menu);

            ApplyLanguage();
            _trayIconManager?.UpdateMenuState(rpc.Status);
        }

        private NotifyIcon trayIcon;
        private ToolStripMenuItem trayMenuDetails, trayMenuState, trayMenuProduce;
        private ToolStripMenuItem trayMenuConnect;
        private ToolStripMenuItem trayMenuPause;
        private ToolStripMenuItem trayMenuDisconnect;
        private ComboBox cmbGameName;
        private Label lblGameAppGuide;
        private System.Windows.Forms.Timer gameAppGuideTimer;
        private ComboBox cmbDetailsType;
        private TextBox txtPName;
        private NumericUpDown numPLevel;
        private ComboBox cmbStateType;
        private ComboBox cmbProduceCharacter;
        private ComboBox cmbCharNameLang;
        private TextBox txtStateCustom;
        private Label lblStartTime;
        private Button btnResetTime;
        private Label lblResetGuide;
        private System.Windows.Forms.Timer resetGuideTimer;
        private ComboBox cmbBtnMode;
        private Label lblBtnModeNote;
        private TextBox txtBtn1Label;
        private TextBox txtBtn1Url;
        private TextBox txtBtn2Label;
        private TextBox txtBtn2Url;
        private Label lblBtnWarning;
        private Button btnConnect;
        private Button btnUpdate;
        private Button btnDisconnect;
        private Label lblStatus;
        private ToolTip statusToolTip;
        private ToolStripMenuItem fileMenu, exitItem;
        private ToolStripMenuItem settingsMenu;
        private ToolStripMenuItem runAtStartupItem;
        private ToolStripMenuItem startMinimizedItem;
        private ToolStripMenuItem autoConnectItem;
        private ToolStripMenuItem checkForUpdatesItem;
        private ToolStripMenuItem monitorItem;
        private ToolStripMenuItem notifyInBackgroundItem;
        private ToolStripMenuItem notifyOnMinimizeItem;
        private ToolStripMenuItem minimizeToTrayItem;
        private ToolStripMenuItem viewMenu;
        private ToolStripMenuItem themeMenu;
        private ToolStripMenuItem themeAuto;
        private ToolStripMenuItem themeLight;
        private ToolStripMenuItem themeDark;
        private ToolStripMenuItem themeOLED;
        private ToolStripMenuItem langMenu;
        private ToolStripMenuItem langEnglish;
        private ToolStripMenuItem langJapanese;
        private ToolStripMenuItem helpMenu;
        private ToolStripMenuItem openAppLocationMenu;
        private ToolStripMenuItem openConfigLocationMenu;
        private ToolStripMenuItem githubMenu;
        private ToolStripMenuItem checkUpdateMenuItem;
        private ToolStripMenuItem aboutMenu;

    }
}
