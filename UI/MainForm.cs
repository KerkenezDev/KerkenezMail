using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
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

        public MainForm()
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;

            // Initialize Core Services
            _configService = new ConfigService();
            _imapService = new ImapService();
            _llamaManager = new LlamaServerManager();
            _llmService = new LlmSummarizerService();

            InitializeComponent();
            string modelName = string.IsNullOrWhiteSpace(_configService.Settings.LlamaModelPath) 
                ? "Not Selected" 
                : Path.GetFileName(_configService.Settings.LlamaModelPath);
            UpdateStatusStrip($"Ready • Model: {modelName}", "Ready");

            // Auto-fetch and auto-summarize unread emails as soon as app opens
            this.Shown += async (s, e) =>
            {
                await Task.Yield();
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

            // Window Dimensions
            this.Size = new Size(1535, 890);
            this.MinimumSize = new Size(1000, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
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
            _sidebar.TabChanged += OnSidebarTabChanged;

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
            _lblMetrics.Text = $"Accounts: {enabledCount} | VRAM: Model Loaded in VRAM";
        }

        private async void OnFormKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.R)
            {
                await _summariesView.FetchAndAutoSummarizeAsync();
                e.Handled = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            _llamaManager.Stop();
        }
    }
}
