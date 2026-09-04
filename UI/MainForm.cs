using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using KerkenezMail.Languages;
using KerkenezMail.Models;
using KerkenezMail.Services;
using KerkenezMail.UI.Controls;
using KerkenezMail.UI.Tabs;

namespace KerkenezMail.UI
{
    public class MainForm : Form
    {
        private readonly ConfigService _configService;
        private readonly ImapService _imapService;
        private readonly LlamaServerManager _llamaManager;
        private readonly LlmSummarizerService _llmService;
        private LiveImapService _liveImapService = null!;

        private SidebarNav _sidebar = null!;
        private Panel _contentPanel = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _lblStatus = null!;
        private ToolStripStatusLabel _lblMetrics = null!;

        private SummariesView _summariesView = null!;
        private AccountsView _accountsView = null!;
        private SettingsView _settingsView = null!;
        private LogsView _logsView = null!;
        private SendMailView? _sendMailView;

        private readonly SmtpService _smtpService;
        private readonly bool _isFirstLaunch;

        public MainForm(ConfigService? configService = null)
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;

            _isFirstLaunch = ConfigService.IsFirstInstallation;

            // Initialize Core Services
            _configService = configService ?? new ConfigService();
            _imapService = new ImapService();
            _smtpService = new SmtpService();
            _llamaManager = new LlamaServerManager();
            _llmService = new LlmSummarizerService();

            InitializeComponent();
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnSystemPowerModeChanged;
            string modelName = _configService.Settings.GetBackendDisplayName();
            UpdateStatusStrip(Lang.Format(StringKeys.StatusReadyBackend, modelName), Lang.T(StringKeys.StatusReady));

            // Auto-fetch and auto-summarize unread emails as soon as app opens
            this.Shown += async (s, e) =>
            {
                await Task.Yield();

                if (_isFirstLaunch && !ShortcutService.ShortcutsExist)
                {
                    try
                    {
                        var res = MessageBox.Show(
                            this,
                            Lang.T(StringKeys.MainShortcutsPromptDesc),
                            Lang.T(StringKeys.MainShortcutsPromptTitle),
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (res == DialogResult.Yes)
                        {
                            ShortcutService.CreateShortcuts();
                        }
                    }
                    catch { }
                }

                await _summariesView.FetchAndAutoSummarizeAsync();
            };

            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLocalization();
            ApplyLocalization();
        }

        private void InitializeComponent()
        {
            // Set window icon for Title Bar and Windows Taskbar
            try
            {
                using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("KerkenezMail.app.ico");
                if (stream != null)
                {
                    this.Icon = new Icon(stream);
                }
                else if (File.Exists("app.ico"))
                {
                    this.Icon = new Icon("app.ico");
                }
                else
                {
                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
            }
            catch
            {
                // Fallback
            }

            // Window Dimensions (Dynamically scaled from settings, defaulting to 60.0% width × 56.0% height of screen working area)
            var currentScreen = Screen.FromPoint(Cursor.Position) ?? Screen.PrimaryScreen;
            var workingArea = currentScreen?.WorkingArea ?? (Screen.PrimaryScreen != null ? Screen.PrimaryScreen.WorkingArea : new Rectangle(0, 0, 1920, 1080));

            double widthScale = _configService.Settings.WindowWidthScale > 0.1 && _configService.Settings.WindowWidthScale <= 1.0
                ? _configService.Settings.WindowWidthScale
                : 0.60;
            double heightScale = _configService.Settings.WindowHeightScale > 0.1 && _configService.Settings.WindowHeightScale <= 1.0
                ? _configService.Settings.WindowHeightScale
                : 0.56;

            int targetWidth = _configService.Settings.WindowWidth >= 960
                ? _configService.Settings.WindowWidth
                : (int)Math.Round(workingArea.Width * widthScale);
            int targetHeight = _configService.Settings.WindowHeight >= 540
                ? _configService.Settings.WindowHeight
                : (int)Math.Round(workingArea.Height * heightScale);

            int minWidth = Math.Min(960, workingArea.Width);
            int minHeight = Math.Min(540, workingArea.Height);

            targetWidth = Math.Clamp(targetWidth, minWidth, workingArea.Width);
            targetHeight = Math.Clamp(targetHeight, minHeight, workingArea.Height);

            this.MinimumSize = new Size(minWidth, minHeight);
            this.Size = new Size(targetWidth, targetHeight);
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(
                workingArea.Left + Math.Max(0, (workingArea.Width - targetWidth) / 2),
                workingArea.Top + Math.Max(0, (workingArea.Height - targetHeight) / 2)
            );
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.KeyPreview = true;

            // 1. Bottom Status Strip
            _statusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(242, 244, 247),
                Font = new Font("Segoe UI", 8.5F),
                Height = 26
            };

            _lblStatus = new ToolStripStatusLabel
            {
                Text = Lang.T(StringKeys.StatusStartingUp),
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(50, 50, 50)
            };

            _lblMetrics = new ToolStripStatusLabel
            {
                Text = "Accounts: 0 | VRAM: Model Loaded in VRAM",
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            _statusStrip.Items.Add(_lblStatus);
            _statusStrip.Items.Add(_lblMetrics);

            // 2. Logs View & Progress Logger
            _logsView = new LogsView();
            var logger = new Progress<string>(msg => _logsView.AppendLog(msg));

            // Initialize Live IMAP Service
            _liveImapService = new LiveImapService(_configService, logger);
            _liveImapService.NewEmailDetected += OnLiveImapNewEmailDetected;

            // 3. Tab Views
            _summariesView = new SummariesView(_configService, _imapService, _llamaManager, _llmService, logger);
            _summariesView.StatusUpdated += (status, vram) => UpdateStatusStrip(status, vram);
            _summariesView.ReplyRequested += (s, email) => OpenReplyScreen(email);

            _accountsView = new AccountsView(_configService, _imapService, logger);
            _accountsView.AccountsChanged += () =>
            {
                _summariesView.RefreshAccountFilter();
                UpdateMetrics();
            };

            _settingsView = new SettingsView(_configService, _llmService, logger);
            _settingsView.SettingsSaved += () =>
            {
                _summariesView.ApplyAiModeLayout();
                UpdateMetrics();
            };

            // 4. Content Panel (Holds active view)
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            _contentPanel.Controls.Add(_summariesView);
            _contentPanel.Controls.Add(_accountsView);
            _contentPanel.Controls.Add(_settingsView);
            _contentPanel.Controls.Add(_logsView);

            // 5. Left Sidebar Navigation
            _sidebar = new SidebarNav();
            _sidebar.IsCollapsed = _configService.Settings.CollapseSidebarByDefault;
            _sidebar.TabChanged += OnSidebarTabChanged;
            _sidebar.MailFolderSelected += OnSidebarMailFolderSelected;
            _sidebar.LiveImapToggled += OnSidebarLiveImapToggled;

            // Initial view
            ShowTab(0);
            UpdateMetrics();

            // Assemble Form
            this.Controls.Add(_contentPanel);
            this.Controls.Add(_sidebar);
            this.Controls.Add(_statusStrip);

            // Keyboard Shortcuts
            this.KeyDown += OnFormKeyDown;
        }

        private async void OnSidebarTabChanged(object? sender, int index)
        {
            ShowTab(index);
            if (index == 0)
            {
                await _summariesView.SwitchToFolderAsync(_sidebar.SelectedFolder);
            }
        }

        private async void OnSidebarMailFolderSelected(object? sender, MailFolderType folder)
        {
            _sidebar.SelectedIndex = 0;
            ShowTab(0);
            await _summariesView.SwitchToFolderAsync(folder);
        }

        private async void OnSidebarLiveImapToggled(object? sender, bool isActive)
        {
            string metrics = _lblMetrics?.Text ?? "";
            if (isActive)
            {
                UpdateStatusStrip(Lang.T(StringKeys.StatusLiveConnecting), metrics);
                await _liveImapService.StartAsync();
                UpdateStatusStrip(_liveImapService.IsRunning ? Lang.T(StringKeys.StatusLiveListening) : Lang.T(StringKeys.StatusLiveStopped), metrics);
            }
            else
            {
                UpdateStatusStrip(Lang.T(StringKeys.StatusLiveDone), metrics);
                await _liveImapService.StopAsync();
                UpdateStatusStrip(Lang.T(StringKeys.StatusLiveStopped), metrics);
            }
        }

        private async void OnLiveImapNewEmailDetected(EmailAccount account, int count)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnLiveImapNewEmailDetected(account, count)));
                return;
            }

            // 1. Toast Notification
            NotificationService.ShowNotification(
                "New Email Received",
                $"{account.Name}: {count} new email(s) received in Inbox.");

            // 2. Status Strip feedback
            UpdateStatusStrip(Lang.Format(StringKeys.StatusLiveNewEmail, account.Name), _lblMetrics?.Text ?? "");

            // 3. Auto-refresh if currently looking at the Inbox folder in Summaries view
            if (_sidebar.SelectedIndex == 0 && _sidebar.SelectedFolder == MailFolderType.Inbox)
            {
                await _summariesView.FetchAndAutoSummarizeAsync(MailFolderType.Inbox);
            }
        }

        private void ShowTab(int index)
        {
            if (index == 1)
            {
                EnsureSendMailViewInitialized();
                if (!_sendMailView!.HasDraft)
                {
                    _sendMailView.SetNewEmail();
                }
            }

            _summariesView.Visible = (index == 0);
            if (_sendMailView != null) _sendMailView.Visible = (index == 1);
            _accountsView.Visible = (index == 2);
            _settingsView.Visible = (index == 3);
            _logsView.Visible = (index == 4);

            if (index == 0)
            {
                _summariesView.ApplyAiModeLayout();
                _summariesView.BringToFront();
            }
            else if (index == 1) _sendMailView!.BringToFront();
            else if (index == 2)
            {
                _accountsView.LoadAccounts();
                _accountsView.BringToFront();
            }
            else if (index == 3)
            {
                _settingsView.LoadSettings();
                _settingsView.BringToFront();
            }
            else if (index == 4) _logsView.BringToFront();
        }

        private void OpenSendMailScreen()
        {
            EnsureSendMailViewInitialized();
            _sendMailView!.SetNewEmail();
            _sidebar.SelectedIndex = 1;
            ShowTab(1);
        }

        private void OpenReplyScreen(EmailItem email)
        {
            EnsureSendMailViewInitialized();
            _sendMailView!.SetReplyEmail(email);
            _sidebar.SelectedIndex = 1;
            ShowTab(1);
        }

        private void EnsureSendMailViewInitialized()
        {
            if (_sendMailView == null)
            {
                _sendMailView = new SendMailView(_configService, _smtpService);
                _sendMailView.BackToInboxRequested += (s, e) =>
                {
                    _sidebar.SelectedIndex = 0;
                    ShowTab(0);
                };
                _sendMailView.EmailSentSuccessfully += (s, e) =>
                {
                    _sidebar.SelectedIndex = 0;
                    ShowTab(0);
                };
                _sendMailView.PopOutRequested += (s, e) =>
                {
                    var popoutForm = new SendMailForm(_configService, _smtpService);
                    _sidebar.SelectedIndex = 0;
                    ShowTab(0);
                    popoutForm.Show(this);
                };
                _contentPanel.Controls.Add(_sendMailView);
            }
        }

        private void ShowSendMailView()
        {
            _sidebar.SelectedIndex = 1;
            ShowTab(1);
        }

        private void UpdateStatusStrip(string status, string vramStatus)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string, string>(UpdateStatusStrip), status, vramStatus);
                return;
            }

            if (_lblStatus != null) _lblStatus.Text = status;
            int enabledCount = _configService.GetAccounts().Count(a => a.IsEnabled);
            if (_lblMetrics != null) _lblMetrics.Text = Lang.Format(StringKeys.StatusAccountsCount, enabledCount, vramStatus);
        }

        private void UpdateMetrics()
        {
            if (_lblMetrics == null) return;
            int enabledCount = _configService.GetAccounts().Count(a => a.IsEnabled);
            string status;
            if (_configService.Settings.IsBatterySaverActive)
            {
                status = Lang.T(StringKeys.StatusBatterySaverNoAi);
            }
            else if (_configService.Settings.IsAiDisabled)
            {
                status = Lang.T(StringKeys.StatusDisabledClassic);
            }
            else
            {
                string backendType = _configService.Settings.AiBackend;
                status = string.Equals(backendType, "LlamaCpp", StringComparison.OrdinalIgnoreCase) 
                    ? (_configService.Settings.InstantVramUnload ? Lang.T(StringKeys.StatusOnDemandVram) : Lang.T(StringKeys.StatusModelLoaded)) 
                    : (string.Equals(backendType, "Ollama", StringComparison.OrdinalIgnoreCase) ? Lang.T(StringKeys.StatusOllamaActive) : Lang.T(StringKeys.StatusCloudActive));
            }
            _lblMetrics.Text = Lang.Format(StringKeys.StatusAccountsBackend, enabledCount, status);
        }

        private void OnSystemPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            if (e.Mode == Microsoft.Win32.PowerModes.StatusChange)
            {
                try
                {
                    if (this.InvokeRequired)
                    {
                        this.BeginInvoke(new Action(() => OnSystemPowerModeChanged(sender, e)));
                        return;
                    }

                    if (_configService.Settings.DisableAiOnBattery)
                    {
                        if (_configService.Settings.IsBatterySaverActive)
                        {
                            _llamaManager.Stop();
                        }

                        _summariesView.ApplyAiModeLayout();
                        UpdateMetrics();
                        _settingsView.UpdateBatteryNotice();
                    }
                }
                catch { }
            }
        }

        private async void OnFormKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.B)
            {
                _sidebar.ToggleCollapsed();
                e.Handled = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.R)
            {
                await _summariesView.FetchAndAutoSummarizeAsync();
                e.Handled = true;
            }
        }

        private bool _isClosingFlushDone = false;

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                Microsoft.Win32.SystemEvents.PowerModeChanged -= OnSystemPowerModeChanged;
            }
            catch { }

            // Stop Live IMAP and transmit RFC 2177 DONE signals to servers immediately
            if (_liveImapService != null && _liveImapService.IsRunning)
            {
                try
                {
                    Task.Run(async () => await _liveImapService.StopAsync()).Wait(2500);
                }
                catch { }
            }

            // Abort active IMAP sync & LLM startup/summaries immediately
            _summariesView.CancelRunningOperations();

            if (!_isClosingFlushDone && _summariesView.HasPendingOrInFlightTriage)
            {
                e.Cancel = true;
                this.Hide(); // Instantly vanishes from user view!

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _summariesView.FlushPendingTriageAsync();
                    }
                    catch { }
                    finally
                    {
                        _isClosingFlushDone = true;
                        _llamaManager.Stop();
                        ConfigService.CleanTempFolder();
                        if (!this.IsDisposed && this.IsHandleCreated)
                        {
                            try
                            {
                                this.BeginInvoke(new Action(() =>
                                {
                                    this.Close();
                                }));
                            }
                            catch { }
                        }
                    }
                });

                return;
            }

            if (this.WindowState == FormWindowState.Normal && this.Width >= 960 && this.Height >= 540)
            {
                _configService.Settings.WindowWidth = this.Width;
                _configService.Settings.WindowHeight = this.Height;
                _configService.SaveConfig();
            }

            base.OnFormClosing(e);
            _llamaManager.Stop();
            ConfigService.CleanTempFolder();
        }

        public void ApplyLocalization()
        {
            this.Text = Lang.T(StringKeys.AppTitle);
            _sidebar?.Invalidate();
            UpdateMetrics();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _summariesView?.CancelRunningOperations();
                _liveImapService?.Dispose();
                _llamaManager?.Dispose();
                ConfigService.CleanTempFolder();
            }
            base.Dispose(disposing);
        }
    }
}
