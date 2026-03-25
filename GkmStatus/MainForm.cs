using DiscordRPC;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Text;
using Button = DiscordRPC.Button;
using static GkmStatus.src.AppConstants;
using static GkmStatus.src.Characters;
using GkmStatus.src;
using static GkmStatus.src.ui.I18n.Text_List;
using GkmStatus.src.ui;
using GkmStatus.src.native;
using static GkmStatus.src.AppSettingsHelper;

namespace GkmStatus
{
    [SupportedOSPlatform("windows")]
    public partial class MainForm : Form
    {
        private readonly bool isInitializing = true;
        private int footerButtonsY;
        private bool _isResettingDefaults;

        private readonly DiscordRpcService rpc = new();
        private bool isManualPaused = false;
        private DateTime startTime;
        private System.Windows.Forms.Timer? connectionTimer;
        private System.Windows.Forms.Timer? clockTimer;
        private int connectionSeconds = 0;
        private const int CONNECTION_TIMEOUT = 30;

        private int lastStateIdx = -1;

        private int lastBtnIdx = -1;
        private DateTime lastUpdateCheck = DateTime.MinValue;

        private readonly float uiScale = 1.0f;

        private readonly UpdateService _updateService = new();
        private readonly ConfigManager _configManager = new();
        private readonly FontManager _fontManager = new();
        private readonly ThemeManager _themeManager;
        private readonly ProcessWatcher _processWatcher;
        private readonly TrayIconManager _trayIconManager;
        private PresenceSettings CurrentPresence => _configManager.Config.Presence;

        public MainForm()
        {
            try
            {
                uiScale = (float)this.DeviceDpi / 96f;
                if (uiScale <= 0) uiScale = 1.0f;
            }
            catch
            {
                uiScale = 1.0f;
            }

            SuspendLayout();
            //InitializeComponent();
            _fontManager.Initialize(uiScale);
            _themeManager = new ThemeManager(_fontManager);
            ApplyComponent();
            ResumeLayout();

            _trayIconManager = new TrayIconManager(this.trayIcon, this.Icon!);

            _processWatcher = new ProcessWatcher(PROCESS_NAME);
            _processWatcher.ProcessStarted += (s, e) =>
            {
                startTime = DateTime.UtcNow;
                UpdateTimestampLabel();
                InitializeRpc();
            };

            _processWatcher.ProcessStopped += (s, e) =>
            {
                PauseRpc();
            };

            rpc.Ready += (s, username) =>
            {
                SafeBeginInvoke(() =>
                {
                    connectionTimer?.Stop();
                    UpdateUIForConnected(username);

                    System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                    {
                        SafeBeginInvoke(UpdateRpc);
                    });
                });
            };

            rpc.StatusChanged += (s, status) =>
            {
                SafeBeginInvoke(() =>
                {
                    if (status == RpcStatus.Error)
                    {
                        HandleConnectionError(rpc.LastErrorMessage ?? "Unknown RPC Error");
                    }
                    else if (status == RpcStatus.Disconnected)
                    {
                        UpdateUIForDisconnect();
                    }
                });
            };


            isInitializing = true;
            _configManager.Load();
            LoadSettings();

            if (startMinimizedItem.Checked)
            {
                this.WindowState = FormWindowState.Minimized;
            }

            UpdateDetailsInputs(null, EventArgs.Empty);
            CmbStateType_SelectedIndexChanged(null, EventArgs.Empty);
            CmbBtnMode_SelectedIndexChanged(null, EventArgs.Empty);

            isInitializing = false;
            InitializeLogic();


            this.FormClosing += (s, e) =>
            {
                if (minimizeToTrayItem?.Checked == true && e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                    this.ShowInTaskbar = false;

                    trayIcon.Visible = true;

                    if (notifyOnMinimizeItem?.Checked == true)
                    {
                        trayIcon.Tag = null;
                        trayIcon.ShowBalloonTip(3000, I18n.T(App_Name), I18n.T(Notify_Minimized), ToolTipIcon.Info);
                    }

                    return;
                }

                SaveSettings();

                connectionTimer?.Stop();
                connectionTimer?.Dispose();
                connectionTimer = null;

                clockTimer?.Stop();
                clockTimer?.Dispose();
                clockTimer = null;

                rpc.Dispose();
                _processWatcher.Dispose();
                _fontManager.Dispose();
                _updateService.Dispose();

                if (trayIcon != null)
                    trayIcon.Visible = false;
                _trayIconManager?.Dispose();
            };

        }

        private void SafeBeginInvoke(Action action)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke(action);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private void RestoreFromTray()
        {
            this.ShowInTaskbar = true;
            this.Show();
            this.WindowState = FormWindowState.Normal;
            trayIcon.Visible = false;
            this.Activate();
            ReapplyTheme();
        }

        private void ReapplyTheme()
        {
            AppTheme theme = AppTheme.Auto;
            if (themeLight.Checked) theme = AppTheme.Light;
            else if (themeDark.Checked) theme = AppTheme.Dark;
            else if (themeOLED.Checked) theme = AppTheme.OLED;

            _themeManager.ApplyTheme(this, theme);
        }

        private void ApplyLanguage()
        {
            this.Text = $"{I18n.T(App_Name)} v{Application.ProductVersion}";

            if (fileMenu != null)
            {
                fileMenu.Text = I18n.T(Menu_File);
                exitItem.Text = I18n.T(Menu_Exit);

                settingsMenu.Text = I18n.T(Menu_Settings);
                runAtStartupItem.Text = I18n.T(Menu_RunAtStartup);
                startMinimizedItem.Text = I18n.T(Menu_StartMinimized);
                autoConnectItem.Text = I18n.T(Menu_AutoConnect);
                checkForUpdatesItem.Text = I18n.T(Menu_CheckForUpdates);
                notifyInBackgroundItem.Text = I18n.T(Menu_NotifyInBackground);
                notifyOnMinimizeItem.Text = I18n.T(Menu_NotifyOnMinimize);
                minimizeToTrayItem.Text = I18n.T(Menu_MinimizeToTray);
                monitorItem.Text = I18n.T(Menu_MonitorProcess);

                viewMenu.Text = I18n.T(Menu_View);
                themeMenu.Text = I18n.T(Menu_Theme);
                themeAuto.Text = I18n.T(Menu_ThemeAuto);
                themeLight.Text = I18n.T(Menu_ThemeLight);
                themeDark.Text = I18n.T(Menu_ThemeDark);
                themeOLED.Text = I18n.T(Menu_ThemeOLED);
                langMenu.Text = I18n.T(Menu_Language);

                helpMenu.Text = I18n.T(Menu_Help);
                openAppLocationMenu.Text = I18n.T(Menu_OpenAppLocation);
                openConfigLocationMenu.Text = I18n.T(Menu_OpenConfigLocation);
                githubMenu.Text = I18n.T(Menu_Github);
                checkUpdateMenuItem.Text = I18n.T(Menu_CheckUpdateNow);
                aboutMenu.Text = I18n.T(Menu_About);
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
                trayIcon.ContextMenuStrip.Items[0].Text = I18n.T(Tray_Open);
                trayMenuDetails?.Text = I18n.T(Header_Details);
                trayMenuState?.Text = I18n.T(Header_State);
                trayMenuProduce?.Text = I18n.T(Tray_ProducingIdol);
                trayMenuConnect.Text = I18n.T(Button_Connect);
                trayMenuPause.Text = I18n.T(Button_Pause);
                trayMenuDisconnect.Text = I18n.T(Button_Disconnect);
                if (trayIcon.ContextMenuStrip.Items.Count > 0) trayIcon.ContextMenuStrip.Items[trayIcon.ContextMenuStrip.Items.Count - 1].Text = I18n.T(Tray_Exit);
            }

            lblResetGuide.Text = I18n.T(Timestamp_Guide);
            lblGameAppGuide.Text = I18n.T(GameApp_Guide);
            lblBtnModeNote?.Text = I18n.T(Button_ModeNote);
            UpdateTimestampLabel();

            btnResetTime.Text = I18n.T(Timestamp_Reset);
            btnUpdate.Text = I18n.T(Button_Update);
            btnDisconnect.Text = I18n.T(Button_Disconnect);

            Native.SetPlaceholder(txtPName, I18n.T(Placeholder_PName));
            Native.SetPlaceholder(txtBtn1Label, I18n.T(Placeholder_BtnLabel, 1));

            if (rpc.Status != RpcStatus.Disconnected)
            {
                if (connectionTimer?.Enabled == true || rpc.Status == RpcStatus.Connecting)
                {
                    lblStatus.Text = I18n.T(Status_Connecting, connectionSeconds);
                }
                else if (rpc.Status == RpcStatus.Connected && !isManualPaused)
                {
                    UpdateUIForConnected(rpc.CurrentUsername ?? "Unknown");
                }
                else
                {
                    UpdateUIForPause();
                    btnConnect.Text = I18n.T(Button_Resume);
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

            RefreshCombo(cmbDetailsType, [I18n.T(Details_None), I18n.T(Details_PName), I18n.T(Details_PLv), I18n.T(Details_Both)]);
            RefreshCombo(cmbStateType, [I18n.T(State_None), I18n.T(State_PID), I18n.T(State_Idol), I18n.T(State_Producing), I18n.T(State_Custom)]);
            RefreshCombo(cmbBtnMode, [I18n.T(Button_ModeNone), I18n.T(Button_ModeStore), I18n.T(Button_ModeApp), I18n.T(Button_ModeCustom)]);
            RefreshCombo(cmbCharNameLang, [I18n.T(CharName_JP), I18n.T(CharName_EN)]);

            CmbStateType_SelectedIndexChanged(null, EventArgs.Empty);
            CmbBtnMode_SelectedIndexChanged(null, EventArgs.Empty);
        }

        private void CmbGameName_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbGameName.SelectedIndex == -1) return;
            CurrentPresence.GameAppIndex = cmbGameName.SelectedIndex;

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
            using var about = new AboutForm(this.BackColor, this.ForeColor, _fontManager.AppFont, uiScale);
            about.ShowDialog(this);
        }

        private async Task CheckForUpdatesAsync(bool manual = false)
        {
            if (!manual && (DateTime.UtcNow - lastUpdateCheck).TotalHours < 24)
                return;

            var result = await _updateService.CheckForUpdatesAsync(Application.ProductVersion);

            if (result.IsRateLimited)
            {
                if (manual) SafeBeginInvoke(() => MessageBox.Show(this, I18n.T(Update_RateLimit), "Update", MessageBoxButtons.OK, MessageBoxIcon.Warning));
                return;
            }

            if (!result.IsSuccess)
            {
                if (manual) SafeBeginInvoke(() => MessageBox.Show(this, "Update check failed: " + result.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
                return;
            }

            lastUpdateCheck = DateTime.UtcNow;
            SaveSettings();

            if (result.HasUpdate)
            {
                SafeBeginInvoke(() =>
                {
                    if (manual)
                    {
                        if (MessageBox.Show(this, I18n.T(Update_NewAvailable, result.LatestVersion), "Update", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            _ = StartupManager.OpenUrl(result.ReleaseUrl);
                        }
                    }
                    else
                    {
                        trayIcon.Visible = true;
                        trayIcon.Tag = result.ReleaseUrl;
                        trayIcon.ShowBalloonTip(5000, I18n.T(Update_NotificationTitle), I18n.T(Update_NotificationBody, result.LatestVersion), ToolTipIcon.Info);
                    }
                });
            }
            else if (manual)
                SafeBeginInvoke(() => MessageBox.Show(this, I18n.T(Update_NoUpdate), "Update", MessageBoxButtons.OK, MessageBoxIcon.Information));
        }

        private void CmbStateType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int idx = cmbStateType.SelectedIndex;
            if (idx == -1) return;
            var stateType = GetStateType(idx);

            if (!isInitializing && lastStateIdx != -1)
            {
                var lastStateType = GetStateType(lastStateIdx);
                if (lastStateType != PresenceStateType.Idol && lastStateType != PresenceStateType.Producing)
                {
                    CurrentPresence.StateHistory[GetStateString(lastStateType)] = txtStateCustom.Text;
                }
            }

            bool isProduceMode = stateType is PresenceStateType.Idol or PresenceStateType.Producing;

            if (txtStateCustom.Tag is Panel container)
            {
                container.Visible = !isProduceMode && stateType != PresenceStateType.None;
            }

            cmbProduceCharacter.Visible = isProduceMode;
            cmbCharNameLang.Visible = isProduceMode;

            if (stateType == PresenceStateType.PID)
            {
                if (txtStateCustom.Tag is Panel con)
                {
                    con.Width = S(165);
                    txtStateCustom.Width = S(165) - S(10);
                    con.Invalidate();
                }
                txtStateCustom.Enabled = true;
                txtStateCustom.MaxLength = 8;
                Native.SetPlaceholder(txtStateCustom, I18n.T(Placeholder_PID));
                if (CurrentPresence.StateHistory.TryGetValue(GetStateString(PresenceStateType.PID), out var pidText))
                    txtStateCustom.Text = pidText;
                else
                    txtStateCustom.Text = "";
            }
            else if (stateType == PresenceStateType.Custom)
            {
                if (txtStateCustom.Tag is Panel con)
                {
                    con.Width = S(320);
                    txtStateCustom.Width = S(320) - S(10);
                    con.Invalidate();
                }
                txtStateCustom.Enabled = true;
                txtStateCustom.MaxLength = 128;
                Native.SetPlaceholder(txtStateCustom, I18n.T(Placeholder_Custom));
                if (CurrentPresence.StateHistory.TryGetValue(GetStateString(PresenceStateType.Custom), out var customText))
                    txtStateCustom.Text = customText;
                else
                    txtStateCustom.Text = "";
            }
            else
            {
                txtStateCustom.Enabled = false;
            }

            if (stateType is PresenceStateType.Idol or PresenceStateType.Producing)
            {
                var currentId = (stateType == PresenceStateType.Idol) ? CurrentPresence.SelectedIdolCharacterId : CurrentPresence.SelectedProduceCharacterId;
                int charIdx = FindProduceCharacterIndex(currentId);
                cmbProduceCharacter.SelectedIndex = charIdx >= 0 ? charIdx : 0;
            }

            lastStateIdx = idx;
            if (!isInitializing) SaveSettings();
        }

        private void CmbBtnMode_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbBtnMode.SelectedIndex == -1) return;
            if (!isInitializing && GetButtonMode(lastBtnIdx) == PresenceButtonMode.Custom)
            {
                CurrentPresence.ButtonHistory[GetButtonModeString(PresenceButtonMode.Custom)] = new ButtonHistoryData { L1 = txtBtn1Label.Text, U1 = txtBtn1Url.Text, L2 = txtBtn2Label.Text, U2 = txtBtn2Url.Text };
            }

            var btnMode = GetButtonMode(cmbBtnMode.SelectedIndex);

            if (btnMode == PresenceButtonMode.None)
            {
                txtBtn1Label.Text = ""; txtBtn1Url.Text = ""; txtBtn2Label.Text = ""; txtBtn2Url.Text = ""; SetBtnInputsEnabled(false);
                lblBtnModeNote.Visible = false;
            }
            else if (btnMode == PresenceButtonMode.Store)
            {
                txtBtn1Label.Text = I18n.T(Button_StoreLabel_Mobile);
                txtBtn1Url.Text = "https://app.adjust.com/1ai6ouao";
                txtBtn2Label.Text = I18n.T(Button_StoreLabel_DMM);
                txtBtn2Url.Text = "https://dmg-gakuen.idolmaster-official.jp/";
                SetBtnInputsEnabled(false);
                lblBtnModeNote.Visible = true;
            }
            else if (btnMode == PresenceButtonMode.App)
            {
                txtBtn1Label.Text = I18n.T(Button_AboutPresence);
                txtBtn1Url.Text = "https://github.com/Wea017net/GkmStatus";
                txtBtn2Label.Text = ""; txtBtn2Url.Text = ""; SetBtnInputsEnabled(false);
                lblBtnModeNote.Visible = true;
            }
            else if (btnMode == PresenceButtonMode.Custom)
            {
                Native.SetPlaceholder(txtBtn1Label, I18n.T(Placeholder_BtnLabel, 1));
                Native.SetPlaceholder(txtBtn1Url, I18n.T(Placeholder_BtnUrl, 1));
                Native.SetPlaceholder(txtBtn2Label, I18n.T(Placeholder_BtnLabel, 2));
                Native.SetPlaceholder(txtBtn2Url, I18n.T(Placeholder_BtnUrl, 2));
                if (CurrentPresence.ButtonHistory.TryGetValue(GetButtonModeString(PresenceButtonMode.Custom), out var h))
                {
                    txtBtn1Label.Text = h.L1; txtBtn1Url.Text = h.U1;
                    txtBtn2Label.Text = h.L2; txtBtn2Url.Text = h.U2;
                }
                else { txtBtn1Label.Text = ""; txtBtn1Url.Text = ""; txtBtn2Label.Text = ""; txtBtn2Url.Text = ""; }
                SetBtnInputsEnabled(true);
                lblBtnWarning.Visible = Encoding.UTF8.GetByteCount(txtBtn1Label.Text) > 32 || Encoding.UTF8.GetByteCount(txtBtn2Label.Text) > 32;
                lblBtnModeNote.Visible = true;
            }
            else
            {
                lblBtnWarning.Visible = false;
            }
            lastBtnIdx = cmbBtnMode.SelectedIndex;
            if (!isInitializing) SaveSettings();
        }

        private void SaveSettings()
        {
            if (isInitializing) return;

            var config = _configManager.Config;
            var settings = config.Settings;
            var presence = config.Presence;

            settings.StartMinimized = startMinimizedItem.Checked;
            settings.ConnectOnStart = autoConnectItem.Checked;
            settings.AutoCheckUpdates = checkForUpdatesItem.Checked;
            settings.ShowBackgroundNotifications = notifyInBackgroundItem.Checked;
            settings.NotifyOnMinimize = notifyOnMinimizeItem.Checked;
            settings.MinimizeToTray = minimizeToTrayItem.Checked;
            settings.AutoDetectGakumas = monitorItem.Checked;
            var selectedTheme = themeLight.Checked ? AppTheme.Light : (themeDark.Checked ? AppTheme.Dark : (themeOLED.Checked ? AppTheme.OLED : AppTheme.Auto));
            settings.SelectedTheme = GetThemeString(selectedTheme);
            settings.SelectedLanguage = GetLangString(GetLanguage(langEnglish.Checked ? 1 : 0));
            settings.LastUpdateCheck = lastUpdateCheck;

            var detailsType = GetDetailsType(cmbDetailsType.SelectedIndex);
            var stateType = GetStateType(cmbStateType.SelectedIndex);
            var buttonMode = GetButtonMode(cmbBtnMode.SelectedIndex);

            presence.DetailsType = GetDetailsString(detailsType);
            presence.GameAppIndex = cmbGameName.SelectedIndex >= 0 ? cmbGameName.SelectedIndex : 0;
            presence.ProducerName = txtPName.Text;
            presence.ProducerLevel = (int)numPLevel.Value;
            presence.StateType = GetStateString(stateType);
            presence.ButtonMode = GetButtonModeString(buttonMode);
            presence.CharNameLangIndex = cmbCharNameLang.SelectedIndex;

            if (cmbProduceCharacter.SelectedIndex >= 0)
            {
                string characterId = ProduceCharacters[cmbProduceCharacter.SelectedIndex].Id;
                if (stateType == PresenceStateType.Idol) presence.SelectedIdolCharacterId = characterId;
                else if (stateType == PresenceStateType.Producing) presence.SelectedProduceCharacterId = characterId;
            }

            _configManager.Save();
        }

        private void LoadSettings()
        {
            var config = _configManager.Config;
            var settings = config.Settings;
            var presence = config.Presence;

            try
            {
                startMinimizedItem.Checked = settings.StartMinimized;
                autoConnectItem.Checked = settings.ConnectOnStart;
                checkForUpdatesItem.Checked = settings.AutoCheckUpdates;
                notifyInBackgroundItem.Checked = settings.ShowBackgroundNotifications;
                notifyOnMinimizeItem.Checked = settings.NotifyOnMinimize;
                minimizeToTrayItem.Checked = settings.MinimizeToTray;
                monitorItem.Checked = settings.AutoDetectGakumas;

                _processWatcher.Enabled = settings.AutoDetectGakumas;
                if (_processWatcher.Enabled)
                {
                    _processWatcher.ForceCheck();
                }

                var theme = GetTheme(settings.SelectedTheme);
                switch (theme)
                {
                    case AppTheme.Light: themeLight.PerformClick(); break;
                    case AppTheme.Dark: themeDark.PerformClick(); break;
                    case AppTheme.OLED: themeOLED.PerformClick(); break;
                    default: themeAuto.PerformClick(); break;
                }

                var language = GetLanguage(settings.SelectedLanguage ?? (System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ja") ? "ja" : "en"));
                if (language == I18n.Language.English)
                {
                    I18n.CurrentLanguage = I18n.Language.English;
                    langEnglish.Checked = true; langJapanese.Checked = false;
                }
                else
                {
                    I18n.CurrentLanguage = I18n.Language.Japanese;
                    langJapanese.Checked = true; langEnglish.Checked = false;
                }

                lastUpdateCheck = settings.LastUpdateCheck;

                cmbGameName.SelectedIndex = Math.Clamp(presence.GameAppIndex, 0, cmbGameName.Items.Count - 1);
                cmbDetailsType.SelectedIndex = GetDetailsIndex(GetDetailsType(presence.DetailsType ?? GetDetailsString(PresenceDetailsType.Both)));
                txtPName.Text = presence.ProducerName;
                numPLevel.Value = Math.Clamp(presence.ProducerLevel, 1, 100);

                var loadedStateType = GetStateType(presence.StateType ?? GetStateString(PresenceStateType.Producing));
                cmbStateType.SelectedIndex = GetStateIndex(loadedStateType);

                if (loadedStateType is not PresenceStateType.Idol and not PresenceStateType.Producing and not PresenceStateType.None)
                {
                    var stateKey = GetStateString(loadedStateType);
                    if (presence.StateHistory.TryGetValue(stateKey, out var savedText))
                        txtStateCustom.Text = savedText;
                }

                cmbCharNameLang.SelectedIndex = Math.Clamp(presence.CharNameLangIndex, 0, 1);

                RefreshProduceCharacterList();
                string? currentId = (GetStateType(cmbStateType.SelectedIndex) == PresenceStateType.Idol)
                    ? presence.SelectedIdolCharacterId
                    : presence.SelectedProduceCharacterId;

                int charIdx = FindProduceCharacterIndex(currentId);
                cmbProduceCharacter.SelectedIndex = charIdx >= 0 ? charIdx : 0;

                cmbBtnMode.SelectedIndex = GetButtonModeIndex(GetButtonMode(presence.ButtonMode ?? GetButtonModeString(PresenceButtonMode.Store)));

                if (settings.ConnectOnStart) InitializeRpc();
                ApplyLanguage();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UI Load Error: " + ex.Message);
                if (!_isResettingDefaults)
                    SetDefaultSettings();
            }
        }

        private void SetDefaultSettings()
        {
            if (_isResettingDefaults)
                return;

            _isResettingDefaults = true;
            try
            {
                _configManager.ResetToDefault();
                LoadSettings();
            }
            finally
            {
                _isResettingDefaults = false;
            }
        }

        private void InitializeLogic()
        {
            startTime = DateTime.UtcNow;
            UpdateTimestampLabel();

            clockTimer = new System.Windows.Forms.Timer { Interval = 1000, Enabled = true };
            clockTimer.Tick += (s, e) =>
            {
                UpdateTimestampLabel();
                if (rpc.IsInitialized == true)
                    rpc.Invoke();
            };
        }

        private void InitializeRpc()
        {
            if (rpc.Status == RpcStatus.Connecting || rpc.Status == RpcStatus.Connected) return;
            if (connectionTimer?.Enabled == true) return;
            if (cmbGameName.SelectedIndex < 0 || cmbGameName.SelectedIndex >= GameApps.Count) return;

            connectionSeconds = 0;
            if (connectionTimer is null)
            {
                connectionTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                connectionTimer.Tick += (s, e) =>
                {
                    if (rpc.Status != RpcStatus.Connecting) return;

                    connectionSeconds++;
                    lblStatus.Text = I18n.T("Status_Connecting", connectionSeconds);
                    if (connectionSeconds >= CONNECTION_TIMEOUT) HandleConnectionError(I18n.T(Status_Timeout));
                };
            }

            lblStatus.Text = I18n.T("Status_Connecting", 0);
            lblStatus.ForeColor = COLOR_PAUSE;
            btnConnect.Enabled = false;
            AdjustStatusVerticalPosition();
            _trayIconManager?.UpdateStatusIcon(COLOR_PAUSE, I18n.T(Tray_Status_Connecting));

            connectionTimer.Start();

            string appId = GameApps[cmbGameName.SelectedIndex].AppId;
            rpc.Initialize(appId);
        }

        private void HandleConnectionError(string message)
        {
            connectionTimer?.Stop();
            lblStatus.Text = message; lblStatus.ForeColor = COLOR_ERROR;
            statusToolTip.SetToolTip(lblStatus, message);
            AdjustStatusVerticalPosition();
            btnConnect.Text = I18n.T(Button_Connect); btnConnect.BackColor = COLOR_CONNECT; btnConnect.Enabled = true;
            btnUpdate.Enabled = false; btnUpdate.BackColor = COLOR_DISABLED;
            btnDisconnect.Enabled = false; btnDisconnect.BackColor = COLOR_DISABLED;
            _trayIconManager?.UpdateStatusIcon(null);
            rpc.Deinitialize();
            _trayIconManager?.UpdateMenuState(rpc.Status);
        }

        private void PauseRpc()
        {
            if (rpc.IsInitialized == true)
                rpc.Clear();
            isManualPaused = true;
            UpdateUIForPause();
            if (this.WindowState == FormWindowState.Minimized && (notifyInBackgroundItem?.Checked == true))
            {
                trayIcon.Tag = null;
                trayIcon.ShowBalloonTip(3000, I18n.T(App_Name), I18n.T(Status_Disconnected_Notify), ToolTipIcon.Info);
            }
        }

        private void DisposeRpc()
        {
            connectionTimer?.Stop();
            rpc.Deinitialize();
            isManualPaused = false;
            UpdateUIForDisconnect();
            if (this.WindowState == FormWindowState.Minimized && (notifyInBackgroundItem?.Checked == true))
            {
                trayIcon.Tag = null;
                trayIcon.ShowBalloonTip(3000, I18n.T(App_Name), I18n.T(Status_ManualDisconnected_Notify), ToolTipIcon.Info);
            }
        }

        private void UpdateUIForConnected(string username)
        {
            isManualPaused = false;
            string status = I18n.T(Status_Connected, username);
            lblStatus.Text = status;
            lblStatus.ForeColor = COLOR_CONNECT;
            statusToolTip.SetToolTip(lblStatus, status.Replace("\n", " "));
            AdjustStatusVerticalPosition();
            btnConnect.Text = I18n.T(Button_Pause);
            btnConnect.BackColor = COLOR_PAUSE;
            btnConnect.Enabled = true;
            btnUpdate.Enabled = true;
            btnUpdate.BackColor = COLOR_PRIMARY;
            btnDisconnect.Enabled = true;
            btnDisconnect.BackColor = COLOR_ERROR;
            btnResetTime.Enabled = true;
            btnResetTime.BackColor = COLOR_PRIMARY;
            _trayIconManager?.UpdateStatusIcon(COLOR_CONNECT, I18n.T(Tray_Status_Connected));
            _trayIconManager?.UpdateMenuState(rpc.Status);
        }

        private void UpdateUIForPause()
        {
            string status = I18n.T(Status_Paused);
            lblStatus.Text = status;
            lblStatus.ForeColor = COLOR_PAUSE;
            statusToolTip.SetToolTip(lblStatus, status.Replace("\n", " "));
            AdjustStatusVerticalPosition();
            btnConnect.Text = I18n.T(Button_Resume);
            btnConnect.BackColor = COLOR_CONNECT;
            btnConnect.Enabled = true;
            btnUpdate.Enabled = false;
            btnUpdate.BackColor = COLOR_DISABLED;
            btnDisconnect.Enabled = true;
            btnDisconnect.BackColor = COLOR_ERROR;
            btnResetTime.Enabled = true;
            btnResetTime.BackColor = COLOR_PRIMARY;
            _trayIconManager?.UpdateStatusIcon(COLOR_PAUSE, I18n.T(Tray_Status_Paused));
            _trayIconManager?.UpdateMenuState(rpc.Status);
        }

        private void UpdateUIForDisconnect()
        {
            connectionTimer?.Stop();
            string status = monitorItem.Checked ? I18n.T(Status_Disconnected_Auto) : I18n.T(Status_Disconnected);
            lblStatus.Text = status;
            lblStatus.ForeColor = Color.Gray;
            statusToolTip.SetToolTip(lblStatus, status.Replace("\n", " "));
            AdjustStatusVerticalPosition();
            btnConnect.Text = I18n.T(Button_Connect);
            btnConnect.BackColor = COLOR_CONNECT;
            btnConnect.Enabled = true;
            btnUpdate.Enabled = false;
            btnUpdate.BackColor = COLOR_DISABLED;
            btnDisconnect.Enabled = false;
            btnDisconnect.BackColor = COLOR_DISABLED;
            btnResetTime.Enabled = false;
            btnResetTime.BackColor = COLOR_DISABLED;
            _trayIconManager?.UpdateStatusIcon(null);
            _trayIconManager?.UpdateMenuState(rpc.Status);
        }

        private void UpdateRpc()
        {
            if (rpc.Status != RpcStatus.Connected)
                return;

            string? pName = string.IsNullOrWhiteSpace(txtPName.Text) ? I18n.T(Placeholder_PName) : txtPName.Text;
            string? details = null;
            var detailsType = GetDetailsType(cmbDetailsType.SelectedIndex);
            switch (detailsType)
            {
                case PresenceDetailsType.PName: details = pName; break;
                case PresenceDetailsType.PLv: details = $"PLv{numPLevel.Value}"; break;
                case PresenceDetailsType.Both: details = $"{pName} | PLv{numPLevel.Value}"; break;
            }
            string? state = null;

            var stateType = GetStateType(cmbStateType.SelectedIndex);

            if (stateType == PresenceStateType.PID)
            {
                state = $"P-ID: {(string.IsNullOrWhiteSpace(txtStateCustom.Text) ? I18n.T(State_NotSet) : txtStateCustom.Text)}";
            }
            else if (stateType is PresenceStateType.Idol or PresenceStateType.Producing)
            {
                string? charId = (stateType == PresenceStateType.Idol) ? CurrentPresence.SelectedIdolCharacterId : CurrentPresence.SelectedProduceCharacterId;
                var pc = ProduceCharacters.FirstOrDefault(c => c.Id == charId);
                if (pc != null)
                {
                    string name = CurrentPresence.CharNameLangIndex == 1 ? pc.NameEn : pc.Display;
                    if (stateType == PresenceStateType.Producing)
                    {
                        state = I18n.T(State_Producing_Format, name);
                    }
                    else
                    {
                        state = I18n.T(State_Idol_Format, name);
                    }
                }
            }
            else if (stateType == PresenceStateType.Custom)
            {
                state = string.IsNullOrWhiteSpace(txtStateCustom.Text) ? "" : txtStateCustom.Text;
            }
            Button[]? buttons = null;
            if (GetButtonMode(cmbBtnMode.SelectedIndex) != PresenceButtonMode.None)
            {

                var b1 = (!string.IsNullOrEmpty(txtBtn1Label.Text) && IsValidRpcButtonUrl(txtBtn1Url.Text)) ? new Button { Label = txtBtn1Label.Text, Url = txtBtn1Url.Text } : null;
                var b2 = (!string.IsNullOrEmpty(txtBtn2Label.Text) && IsValidRpcButtonUrl(txtBtn2Url.Text)) ? new Button { Label = txtBtn2Label.Text, Url = txtBtn2Url.Text } : null;
                if (b1 != null && b2 != null) buttons = [b1, b2]; else if (b1 != null) buttons = [b1]; else if (b2 != null) buttons = [b2];
            }
            if (!string.IsNullOrEmpty(details) && details.Length < 2) details = "";
            if (!string.IsNullOrEmpty(state) && state.Length < 2) state = "";

            try
            {
                rpc.UpdatePresence(new RichPresence
                {
                    Details = details ?? "",
                    State = state ?? "",
                    Assets = new Assets
                    {
                        LargeImageKey = "app",
                        LargeImageText = $"{I18n.T(App_Name)} v{Application.ProductVersion}"
                    },
                    Buttons = buttons,
                    Timestamps = new Timestamps(startTime)
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RPC Update Error: " + ex.Message);
            }
        }

        private void UpdateDetailsInputs(object? sender, EventArgs e)
        {
            var detailsType = GetDetailsType(cmbDetailsType.SelectedIndex);
            txtPName.Enabled = (detailsType == PresenceDetailsType.PName || detailsType == PresenceDetailsType.Both);
            numPLevel.Enabled = (detailsType == PresenceDetailsType.PLv || detailsType == PresenceDetailsType.Both);
            if (!isInitializing) SaveSettings();
        }
        private void SetBtnInputsEnabled(bool e) { txtBtn1Label.Enabled = e; txtBtn1Url.Enabled = e; txtBtn2Label.Enabled = e; txtBtn2Url.Enabled = e; }
        private void UpdateTimestampLabel()
        {
            bool isSessionActive = rpc.Status is RpcStatus.Connected or RpcStatus.Paused;
            TimeSpan ts = isSessionActive ? DateTime.UtcNow - startTime : TimeSpan.Zero;

            lblStartTime.Text = $"{I18n.T(Timestamp_Label)}: {(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            lblStartTime.ForeColor = this.BackColor.GetBrightness() > 0.5f ? Color.Black : Color.LightGray;
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
            string? currentId = (GetStateType(cmbStateType.SelectedIndex) == PresenceStateType.Idol) ? CurrentPresence.SelectedIdolCharacterId : CurrentPresence.SelectedProduceCharacterId;
            cmbProduceCharacter.Items.Clear();
            bool isEn = CurrentPresence.CharNameLangIndex == 1;

            foreach (var c in ProduceCharacters)
            {
                cmbProduceCharacter.Items.Add(isEn ? c.NameEn : c.Display);
            }

            int idx = FindProduceCharacterIndex(currentId);
            cmbProduceCharacter.SelectedIndex = idx >= 0 ? idx : 0;
        }
    }
}