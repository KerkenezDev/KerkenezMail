using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EmailSummarizer.Models;
using EmailSummarizer.Services;
using EmailSummarizer.UI.Controls;
using EmailSummarizer.UI.Tabs;

namespace EmailSummarizer.UI
{
    public class MainForm : Form
    {
        private readonly ConfigService _configService;
        private readonly ImapService _imapService;
        private readonly LlamaServerManager _llamaManager;
        private readonly LlmSummarizerService _llmService;

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
            string modelName = _configService.Settings.GetBackendDisplayName();
            UpdateStatusStrip($"Ready • Backend: {modelName}", "Ready");

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
                            "Welcome to Email Summarizer!\r\n\r\nWould you like to create shortcuts on your Desktop and Start Menu for easy access?",
                            "Email Summarizer Shortcuts",
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
        }

        private void InitializeComponent()
        {
            this.Text = "Email Summarizer (Win32)";
            
            // Set window icon for Title Bar and Windows Taskbar
            try
            {
                using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("EmailSummarizer.app.ico");
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

            int targetWidth = (int)Math.Round(workingArea.Width * widthScale);
            int targetHeight = (int)Math.Round(workingArea.Height * heightScale);

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
                Text = "Starting up...",
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
            _sidebar.SendMailRequested += (s, e) => OpenSendMailScreen();

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

        private void OnSidebarTabChanged(object? sender, int index)
        {
            ShowTab(index);
        }

        private void ShowTab(int index)
        {
            if (_sendMailView != null) _sendMailView.Visible = false;

            _summariesView.Visible = (index == 0);
            _accountsView.Visible = (index == 1);
            _settingsView.Visible = (index == 2);
            _logsView.Visible = (index == 3);

            if (index == 0) _summariesView.BringToFront();
            else if (index == 1)
            {
                _accountsView.LoadAccounts();
                _accountsView.BringToFront();
            }
            else if (index == 2)
            {
                _settingsView.LoadSettings();
                _settingsView.BringToFront();
            }
            else if (index == 3) _logsView.BringToFront();
        }

        private void OpenSendMailScreen()
        {
            EnsureSendMailViewInitialized();
            _sendMailView!.SetNewEmail();
            ShowSendMailView();
        }

        private void OpenReplyScreen(EmailItem email)
        {
            EnsureSendMailViewInitialized();
            _sendMailView!.SetReplyEmail(email);
            ShowSendMailView();
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
            _summariesView.Visible = false;
            _accountsView.Visible = false;
            _settingsView.Visible = false;
            _logsView.Visible = false;

            if (_sendMailView != null)
            {
                _sendMailView.Visible = true;
                _sendMailView.BringToFront();
            }
        }

        private void UpdateStatusStrip(string status, string vramStatus)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string, string>(UpdateStatusStrip), status, vramStatus);
                return;
            }

            _lblStatus.Text = status;
            int enabledCount = _configService.GetAccounts().Count(a => a.IsEnabled);
            _lblMetrics.Text = $"Accounts: {enabledCount} | VRAM: {vramStatus}";
        }

        private void UpdateMetrics()
        {
            int enabledCount = _configService.GetAccounts().Count(a => a.IsEnabled);
            string backendType = _configService.Settings.AiBackend;
            string status = string.Equals(backendType, "LlamaCpp", StringComparison.OrdinalIgnoreCase) 
                ? (_configService.Settings.InstantVramUnload ? "On-Demand (VRAM Unload)" : "Model Loaded in VRAM") 
                : (string.Equals(backendType, "Ollama", StringComparison.OrdinalIgnoreCase) ? "Ollama Active" : "Cloud Active");
            _lblMetrics.Text = $"Accounts: {enabledCount} | Backend: {status}";
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

            base.OnFormClosing(e);
            _llamaManager.Stop();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _summariesView?.CancelRunningOperations();
                _llamaManager?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
