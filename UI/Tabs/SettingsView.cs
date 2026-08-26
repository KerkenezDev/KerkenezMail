using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using EmailSummarizer.Models;
using EmailSummarizer.Services;

namespace EmailSummarizer.UI.Tabs
{
    public class SettingsView : UserControl
    {
        private readonly ConfigService _configService;
        private readonly LlmSummarizerService _llmService;
        private readonly IProgress<string> _logger;

        // Model controls
        private TextBox _txtModelPath = null!;
        private Button _btnBrowseModel = null!;
        private NumericUpDown _numPort = null!;
        private NumericUpDown _numGpuLayers = null!;
        private TextBox _txtServerUrl = null!;
        private CheckBox _chkAutoStart = null!;
        private CheckBox _chkInstantVram = null!;
        private Button _btnTestLlm = null!;
        private Label _lblLlmTestResult = null!;

        // Email controls
        private NumericUpDown _numMaxEmails = null!;
        private CheckBox _chkOnlyUnread = null!;
        private CheckBox _chkMarkAsSeen = null!;

        // Prompt
        private TextBox _txtPrompt = null!;

        // Buttons
        private Button _btnSave = null!;
        private Button _btnReset = null!;

        private FlowLayoutPanel _mainFlow = null!;

        public SettingsView(ConfigService configService, LlmSummarizerService llmService, IProgress<string> logger)
        {
            _configService = configService;
            _llmService = llmService;
            _logger = logger;

            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(24, 18, 24, 24)
            };

            _mainFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0)
            };

            const int ContentW = 760;

            // ==================== 1. AI Engine Section ====================
            var pnlLlmCard = CreateCardPanel(ContentW);
            
            var lblSec1 = CreateSectionHeader("🤖  Local AI & llama.cpp Engine");
            
            var lblModel = new Label { Text = "GGUF Model File Path:", AutoSize = true, Margin = new Padding(0, 0, 0, 3), ForeColor = Color.FromArgb(50, 50, 50) };
            
            var rowModel = new TableLayoutPanel
            {
                Width = ContentW - 28,
                Height = 32,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 10)
            };
            rowModel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rowModel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _txtModelPath = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 2, 8, 0) };
            _btnBrowseModel = new Button
            {
                Text = "Browse...",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 4, 12, 4),
                Margin = new Padding(0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnBrowseModel.Click += OnBrowseModelClick;

            rowModel.Controls.Add(_txtModelPath, 0, 0);
            rowModel.Controls.Add(_btnBrowseModel, 1, 0);

            // Row for GPU Layers & Server Port (Visible, clearly styled flow layout)
            var rowParams = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 10)
            };

            var pnlLayers = new FlowLayoutPanel
            {
                Width = 200,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 0, 20, 0)
            };
            var lblLayers = new Label { Text = "GPU Layers (-ngl):", AutoSize = true, Font = new Font("Segoe UI", 8.75F), Margin = new Padding(0, 0, 0, 4) };
            _numGpuLayers = new NumericUpDown
            {
                Width = 150,
                Height = 28,
                Minimum = 0,
                Maximum = 999,
                Value = 99,
                Font = new Font("Segoe UI", 9.5F)
            };
            pnlLayers.Controls.Add(lblLayers);
            pnlLayers.Controls.Add(_numGpuLayers);

            var pnlPort = new FlowLayoutPanel
            {
                Width = 200,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0)
            };
            var lblPort = new Label { Text = "Server Port:", AutoSize = true, Font = new Font("Segoe UI", 8.75F), Margin = new Padding(0, 0, 0, 4) };
            _numPort = new NumericUpDown
            {
                Width = 150,
                Height = 28,
                Minimum = 1024,
                Maximum = 65535,
                Value = 8080,
                Font = new Font("Segoe UI", 9.5F)
            };
            _numPort.ValueChanged += (s, e) =>
            {
                _txtServerUrl.Text = $"http://127.0.0.1:{_numPort.Value}/v1/chat/completions";
            };
            pnlPort.Controls.Add(lblPort);
            pnlPort.Controls.Add(_numPort);

            rowParams.Controls.Add(pnlLayers);
            rowParams.Controls.Add(pnlPort);

            // Endpoint URL
            var lblUrl = new Label { Text = "OpenAI Chat Endpoint URL:", AutoSize = true, Margin = new Padding(0, 0, 0, 3), Font = new Font("Segoe UI", 8.75F) };
            _txtServerUrl = new TextBox { Width = ContentW - 28, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 0, 0, 8) };

            _chkAutoStart = new CheckBox
            {
                Text = "Auto-start llama-server on demand",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 0, 0, 3),
                Font = new Font("Segoe UI", 9F)
            };

            _chkInstantVram = new CheckBox
            {
                Text = "Instant VRAM Unload (free GPU memory when batch finishes; uncheck to keep model warm in memory)",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(0, 0, 0, 8),
                Font = new Font("Segoe UI", 9F)
            };

            var rowTestLlm = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 2, 0, 0)
            };

            _btnTestLlm = new Button
            {
                Text = "⚡ Test LLM Connection",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 5, 12, 5),
                Margin = new Padding(0, 0, 10, 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnTestLlm.Click += OnTestLlmClick;

            _lblLlmTestResult = new Label
            {
                Text = "",
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 0),
                Font = new Font("Segoe UI", 8.75F)
            };

            rowTestLlm.Controls.Add(_btnTestLlm);
            rowTestLlm.Controls.Add(_lblLlmTestResult);

            pnlLlmCard.Controls.Add(lblSec1);
            pnlLlmCard.Controls.Add(lblModel);
            pnlLlmCard.Controls.Add(rowModel);
            pnlLlmCard.Controls.Add(rowParams);
            pnlLlmCard.Controls.Add(lblUrl);
            pnlLlmCard.Controls.Add(_txtServerUrl);
            pnlLlmCard.Controls.Add(_chkAutoStart);
            pnlLlmCard.Controls.Add(_chkInstantVram);
            pnlLlmCard.Controls.Add(rowTestLlm);

            // ==================== 2. Email Options Section ====================
            var pnlEmailCard = CreateCardPanel(ContentW);
            
            var lblSec2 = CreateSectionHeader("📬  Email Fetching Configuration");

            var rowMax = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 6)
            };
            var lblMax = new Label { Text = "Max Emails per Account:", AutoSize = true, Margin = new Padding(0, 4, 8, 0), Font = new Font("Segoe UI", 9F) };
            _numMaxEmails = new NumericUpDown { Width = 75, Minimum = 1, Maximum = 100, Value = 15, Font = new Font("Segoe UI", 9F) };
            rowMax.Controls.Add(lblMax);
            rowMax.Controls.Add(_numMaxEmails);

            _chkOnlyUnread = new CheckBox
            {
                Text = "Fetch only unread messages (uncheck to pull all recent emails from INBOX)",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(0, 0, 0, 3),
                Font = new Font("Segoe UI", 9F)
            };

            _chkMarkAsSeen = new CheckBox
            {
                Text = "Mark emails as read on IMAP server upon fetching (\\Seen flag)",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(0, 0, 0, 0),
                Font = new Font("Segoe UI", 9F)
            };

            pnlEmailCard.Controls.Add(lblSec2);
            pnlEmailCard.Controls.Add(rowMax);
            pnlEmailCard.Controls.Add(_chkOnlyUnread);
            pnlEmailCard.Controls.Add(_chkMarkAsSeen);

            // ==================== 3. Prompt Template Section ====================
            var pnlPromptCard = CreateCardPanel(ContentW);
            
            var lblSec3 = CreateSectionHeader("✍️  AI System Prompt Template");

            _txtPrompt = new TextBox
            {
                Width = ContentW - 28,
                Height = 120,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0)
            };

            pnlPromptCard.Controls.Add(lblSec3);
            pnlPromptCard.Controls.Add(_txtPrompt);

            // ==================== 4. Bottom Action Buttons ====================
            var pnlButtons = new FlowLayoutPanel
            {
                Width = ContentW,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 4, 0, 20)
            };

            _btnSave = new Button
            {
                Text = "💾 Save Settings",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(14, 6, 14, 6),
                Margin = new Padding(0, 0, 10, 0),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSave.Click += OnSaveSettingsClick;

            _btnReset = new Button
            {
                Text = "↺ Reset Defaults",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(14, 6, 14, 6),
                Margin = new Padding(0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnReset.Click += OnResetDefaultsClick;

            pnlButtons.Controls.Add(_btnSave);
            pnlButtons.Controls.Add(_btnReset);

            _mainFlow.Controls.Add(pnlLlmCard);
            _mainFlow.Controls.Add(pnlEmailCard);
            _mainFlow.Controls.Add(pnlPromptCard);
            _mainFlow.Controls.Add(pnlButtons);

            scrollPanel.Controls.Add(_mainFlow);
            this.Controls.Add(scrollPanel);
        }

        private static FlowLayoutPanel CreateCardPanel(int width)
        {
            var pnl = new FlowLayoutPanel
            {
                Width = width,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.White,
                Padding = new Padding(14, 12, 14, 12),
                Margin = new Padding(0, 0, 0, 14)
            };

            pnl.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawRectangle(p, 0, 0, pnl.Width - 1, pnl.Height - 1);
            };

            return pnl;
        }

        private static Label CreateSectionHeader(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 20, 20),
                Margin = new Padding(0, 0, 0, 10)
            };
        }

        public void LoadSettings()
        {
            var s = _configService.Settings;
            _txtModelPath.Text = s.LlamaModelPath;
            
            // Explicit range-checked assignment
            _numPort.Value = Math.Max(_numPort.Minimum, Math.Min(_numPort.Maximum, s.LlamaServerPort));
            _numGpuLayers.Value = Math.Max(_numGpuLayers.Minimum, Math.Min(_numGpuLayers.Maximum, s.LlamaGpuLayers));
            
            _txtServerUrl.Text = s.LlamaServerUrl;
            _chkAutoStart.Checked = s.AutoStartLlamaServer;
            _chkInstantVram.Checked = s.InstantVramUnload;

            _numMaxEmails.Value = Math.Max(_numMaxEmails.Minimum, Math.Min(_numMaxEmails.Maximum, s.MaxEmailsPerAccount));
            _chkOnlyUnread.Checked = s.OnlyUnread;
            _chkMarkAsSeen.Checked = s.MarkAsSeen;

            _txtPrompt.Text = s.SystemPrompt;
        }

        private void OnBrowseModelClick(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "GGUF Model Files (*.gguf)|*.gguf|All Files (*.*)|*.*",
                Title = "Select GGUF Model File"
            };

            if (File.Exists(_txtModelPath.Text))
            {
                ofd.InitialDirectory = Path.GetDirectoryName(_txtModelPath.Text);
            }

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _txtModelPath.Text = ofd.FileName;
            }
        }

        private async void OnTestLlmClick(object? sender, EventArgs e)
        {
            _btnTestLlm.Enabled = false;
            _lblLlmTestResult.ForeColor = Color.DarkOrange;
            _lblLlmTestResult.Text = "Testing connection...";

            bool isOnline = await _llmService.TestLlmConnectionAsync(_txtServerUrl.Text.Trim());

            _btnTestLlm.Enabled = true;
            if (isOnline)
            {
                _lblLlmTestResult.ForeColor = Color.DarkGreen;
                _lblLlmTestResult.Text = "✓ LLM endpoint is active and responding!";
            }
            else
            {
                _lblLlmTestResult.ForeColor = Color.Red;
                _lblLlmTestResult.Text = "✗ Could not reach LLM endpoint. Is the server running?";
            }
        }

        private void OnSaveSettingsClick(object? sender, EventArgs e)
        {
            var s = _configService.Settings;
            s.LlamaModelPath = _txtModelPath.Text.Trim();
            s.LlamaServerPort = (int)_numPort.Value;
            s.LlamaGpuLayers = (int)_numGpuLayers.Value;
            s.LlamaServerUrl = _txtServerUrl.Text.Trim();
            s.AutoStartLlamaServer = _chkAutoStart.Checked;
            s.InstantVramUnload = _chkInstantVram.Checked;

            s.MaxEmailsPerAccount = (int)_numMaxEmails.Value;
            s.OnlyUnread = _chkOnlyUnread.Checked;
            s.MarkAsSeen = _chkMarkAsSeen.Checked;

            s.SystemPrompt = _txtPrompt.Text;

            if (_configService.SaveConfig())
            {
                MessageBox.Show("Settings saved successfully!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _logger.Report("[✓] Configuration saved to config.json.");
            }
            else
            {
                MessageBox.Show("Failed to save settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnResetDefaultsClick(object? sender, EventArgs e)
        {
            if (MessageBox.Show("Reset all settings to default values?", "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var defaults = AppSettings.CreateDefault();
                defaults.Accounts = _configService.Settings.Accounts;
                _configService.SaveConfig(defaults);
                LoadSettings();
            }
        }
    }
}
