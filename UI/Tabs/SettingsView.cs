using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KerkenezMail.Languages;
using KerkenezMail.Models;
using KerkenezMail.Services;

namespace KerkenezMail.UI.Tabs
{
    public class SettingsView : UserControl
    {
        private readonly ConfigService _configService;
        private readonly LlmSummarizerService _llmService;
        private readonly IProgress<string> _logger;

        // AI Backend Selection & Containers
        public event Action? SettingsSaved;

        private ComboBox _cboAiBackend = null!;
        private FlowLayoutPanel _barBackendOptions = null!;
        private Button _btnBackendLlama = null!;
        private Button _btnBackendOllama = null!;
        private Button _btnBackendCloud = null!;
        private Button _btnBackendNoAi = null!;
        private Panel _pnlBatteryActiveWarning = null!;
        private FlowLayoutPanel _pnlLlamaContainer = null!;
        private FlowLayoutPanel _pnlOllamaContainer = null!;
        private FlowLayoutPanel _pnlCloudContainer = null!;
        private FlowLayoutPanel _pnlNoAiContainer = null!;
        private FlowLayoutPanel _pnlGlobalParams = null!;
        private Label _lblTokenTip = null!;
        private FlowLayoutPanel _pnlEmailLengthContainer = null!;
        private FlowLayoutPanel _rowTestLlm = null!;

        // 1.5. Battery Saver controls
        private CheckBox _chkDisableAiOnBattery = null!;
        private Label _lblBatteryStatusBadge = null!;

        // 1. llama.cpp controls
        private TextBox _txtModelPath = null!;
        private Button _btnBrowseModel = null!;
        private NumericUpDown _numPort = null!;
        private NumericUpDown _numGpuLayers = null!;
        private NumericUpDown _numContextSize = null!;
        private TextBox _txtServerUrl = null!;
        private CheckBox _chkAutoStart = null!;
        private CheckBox _chkInstantVram = null!;

        // 2. Ollama controls
        private TextBox _txtOllamaUrl = null!;
        private TextBox _txtOllamaModel = null!;

        // 3. Cloud controls
        private ComboBox _cboCloudPreset = null!;
        private TextBox _txtCloudUrl = null!;
        private TextBox _txtCloudApiKey = null!;
        private TextBox _txtCloudModel = null!;
        private Button _btnToggleKeyVisibility = null!;
        private bool _isApiKeyVisible = false;

        // 4. Global LLM controls
        private NumericUpDown _numTemperature = null!;
        private NumericUpDown _numMaxTokens = null!;
        private NumericUpDown _numMaxSummaryChars = null!;
        private CheckBox _chkUnlimitedEmailChars = null!;

        // Test LLM controls
        private Button _btnTestLlm = null!;
        private Label _lblLlmTestResult = null!;

        // Email controls
        private NumericUpDown _numMaxEmails = null!;
        private CheckBox _chkOnlyUnread = null!;
        private CheckBox _chkMarkAsSeen = null!;
        private ComboBox _cboMultiSelectPreview = null!;
        private TextBox _txtAttachmentDownloadPath = null!;

        // Language & Region controls
        private Label _lblSecLanguage = null!;
        private Label _lblLanguageDesc = null!;
        private ComboBox _cboLanguage = null!;

        // UI & Layout controls
        private CheckBox _chkCollapseSidebarByDefault = null!;
        private NumericUpDown _numWindowWidthScale = null!;
        private NumericUpDown _numWindowHeightScale = null!;
        private Label _lblScalePreview = null!;
        private Button _btnApplyWindowSizeNow = null!;

        // System Tray & Notification controls
        private CheckBox _chkAlwaysKeepOn = null!;
        private CheckBox _chkEnableTrayNotifs = null!;
        private NumericUpDown _numTrayInterval = null!;
        private CheckBox _chkStartWithWindows = null!;
        private Button _btnRestartDaemon = null!;
        private Label _lblDaemonStatus = null!;

        // Prompt
        private TextBox _txtPrompt = null!;

        // Buttons
        private Button _btnSave = null!;
        private Button _btnReset = null!;

        // Section headers & labels for dynamic localization
        private Label _lblSecLlm = null!;
        private Label _lblBackend = null!;
        private Label _lblWarnTitle = null!;
        private Label _lblWarnDesc = null!;
        private Label _lblModel = null!;
        private Label _lblLayers = null!;
        private Label _lblPort = null!;
        private Label _lblContextSize = null!;
        private Label _lblUrl = null!;
        private Label _lblOllamaInfo = null!;
        private Label _lblOllamaUrl = null!;
        private Label _lblOllamaModel = null!;
        private Label _lblSuggestions = null!;
        private Label _lblPreset = null!;
        private Label _lblCloudUrl = null!;
        private Label _lblApiKey = null!;
        private Label _lblCloudModel = null!;
        private Label _lblNoticeHeader = null!;
        private Label _lblNoticeText = null!;
        private Label _lblTemp = null!;
        private Label _lblTempDesc = null!;
        private Label _lblTokens = null!;
        private Label _lblTokensDesc = null!;
        private Label _lblSummaryCharsHeader = null!;
        private Label _lblSummaryCharsDesc = null!;
        private Label _lblCharPresets = null!;
        private Button _btnPreset4k = null!;
        private Button _btnPreset8k = null!;
        private Button _btnPreset16k = null!;
        private Button _btnPreset32k = null!;
        private Button _btnPresetInf = null!;
        private Label _lblSecBattery = null!;
        private Label _lblBatteryDesc = null!;
        private Label _lblSecEmail = null!;
        private Label _lblMaxEmails = null!;
        private Label _lblPreview = null!;
        private Label _lblSecAttachments = null!;
        private Label _lblDownloadPathHeader = null!;
        private Label _lblDownloadPathDesc = null!;
        private Button _btnBrowsePath = null!;
        private Button _btnResetPath = null!;
        private Label _lblSecUi = null!;
        private Label _lblScalingHeader = null!;
        private Label _lblScalingDesc = null!;
        private Label _lblWidth = null!;
        private Label _lblHeight = null!;
        private Label _lblLayoutPresets = null!;
        private Button _btnPresetDefault = null!;
        private Button _btnPresetCompact = null!;
        private Button _btnPresetLarge = null!;
        private Button _btnPresetMax = null!;
        private Button _btnCreateShortcuts = null!;
        private Label _lblSecTray = null!;
        private Label _lblInterval = null!;
        private Label _lblSecPrompt = null!;

        private FlowLayoutPanel _mainFlow = null!;

        public SettingsView(ConfigService configService, LlmSummarizerService llmService, IProgress<string> logger)
        {
            _configService = configService;
            _llmService = llmService;
            _logger = logger;

            InitializeComponent();
            LoadSettings();
            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLocalization();
            ApplyLocalization();
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
            
            _lblSecLlm = CreateSectionHeader("🤖  AI Engine & LLM Backend");

            _lblBackend = new Label
            {
                Text = "Select Inference Backend:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 0, 0, 6)
            };

            // 4 Top Bar Backend Option Buttons (side-by-side)
            _barBackendOptions = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 2, 0, 8),
                Margin = new Padding(0, 0, 0, 10)
            };

            _btnBackendLlama = CreateBackendOptionButton("🦙  Local llama.cpp", 0);
            _btnBackendOllama = CreateBackendOptionButton("🦙  Local Ollama", 1);
            _btnBackendCloud = CreateBackendOptionButton("☁️  Cloud / Custom API", 2);
            _btnBackendNoAi = CreateBackendOptionButton("🚫  No AI (Disabled)", 3);

            _barBackendOptions.Controls.Add(_btnBackendLlama);
            _barBackendOptions.Controls.Add(_btnBackendOllama);
            _barBackendOptions.Controls.Add(_btnBackendCloud);
            _barBackendOptions.Controls.Add(_btnBackendNoAi);

            // ------------------ Battery Saver Active Warning ------------------
            _pnlBatteryActiveWarning = new Panel
            {
                Width = ContentW - 28,
                Height = 88,
                BackColor = Color.FromArgb(254, 249, 231),
                Margin = new Padding(0, 2, 0, 12),
                Visible = false
            };
            _pnlBatteryActiveWarning.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(245, 190, 80), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, _pnlBatteryActiveWarning.Width - 1, _pnlBatteryActiveWarning.Height - 1);
            };

            _lblWarnTitle = new Label
            {
                Text = "⚡  Running in No AI Mode (Battery Saver Active)",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(145, 95, 0),
                Location = new Point(14, 10),
                AutoSize = true
            };

            _lblWarnDesc = new Label
            {
                Text = "AI summarization and priority ranking are temporarily suspended because this device is running on battery power. Your configured settings below remain saved and will automatically resume when plugged into AC power.",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(115, 75, 0),
                Location = new Point(14, 30),
                AutoSize = true,
                MaximumSize = new Size(ContentW - 56, 0)
            };

            _pnlBatteryActiveWarning.Controls.Add(_lblWarnTitle);
            _pnlBatteryActiveWarning.Controls.Add(_lblWarnDesc);

            void AdjustWarningHeight()
            {
                _pnlBatteryActiveWarning.Height = Math.Max(84, _lblWarnDesc.Bottom + 12);
            }
            _lblWarnDesc.SizeChanged += (s, e) => AdjustWarningHeight();
            _pnlBatteryActiveWarning.VisibleChanged += (s, e) => AdjustWarningHeight();
            AdjustWarningHeight();

            _cboAiBackend = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ContentW - 28,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Margin = new Padding(0),
                Visible = false
            };
            _cboAiBackend.Items.Add("🦙  Local llama.cpp (Embedded GGUF)");
            _cboAiBackend.Items.Add("🦙  Local Ollama (localhost:11434)");
            _cboAiBackend.Items.Add("☁️  Cloud / Custom API (OpenAI, OpenRouter, Groq, DeepSeek)");
            _cboAiBackend.Items.Add("🚫  No AI (Disable Priority & Summarizing)");
            _cboAiBackend.SelectedIndex = 0;
            _cboAiBackend.SelectedIndexChanged += (s, e) => UpdateAiBackendPanelsVisibility();

            // ------------------ 1A. llama.cpp Container ------------------
            _pnlLlamaContainer = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 4)
            };

            _lblModel = new Label { Text = "GGUF Model File Path:", AutoSize = true, Margin = new Padding(0, 0, 0, 3), ForeColor = Color.FromArgb(50, 50, 50) };
            
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
            _lblLayers = new Label { Text = "GPU Layers (-ngl):", AutoSize = true, Font = new Font("Segoe UI", 8.75F), Margin = new Padding(0, 0, 0, 4) };
            _numGpuLayers = new NumericUpDown
            {
                Width = 150,
                Height = 28,
                Minimum = 0,
                Maximum = 999,
                Value = 99,
                Font = new Font("Segoe UI", 9.5F)
            };
            pnlLayers.Controls.Add(_lblLayers);
            pnlLayers.Controls.Add(_numGpuLayers);

            var pnlPort = new FlowLayoutPanel
            {
                Width = 200,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0)
            };
            _lblPort = new Label { Text = "Server Port:", AutoSize = true, Font = new Font("Segoe UI", 8.75F), Margin = new Padding(0, 0, 0, 4) };
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
            pnlPort.Controls.Add(_lblPort);
            pnlPort.Controls.Add(_numPort);

            var pnlContextSize = new FlowLayoutPanel
            {
                Width = 200,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 0, 20, 0)
            };
            _lblContextSize = new Label { Text = "Context Size (-c):", AutoSize = true, Font = new Font("Segoe UI", 8.75F), Margin = new Padding(0, 0, 0, 4) };
            _numContextSize = new NumericUpDown
            {
                Width = 150,
                Height = 28,
                Minimum = 2048,
                Maximum = 131072,
                Increment = 1024,
                Value = 8192,
                Font = new Font("Segoe UI", 9.5F)
            };
            pnlContextSize.Controls.Add(_lblContextSize);
            pnlContextSize.Controls.Add(_numContextSize);

            rowParams.WrapContents = true;
            rowParams.Controls.Add(pnlLayers);
            rowParams.Controls.Add(pnlPort);
            rowParams.Controls.Add(pnlContextSize);

            _lblUrl = new Label { Text = "OpenAI Chat Endpoint URL:", AutoSize = true, Margin = new Padding(0, 0, 0, 3), Font = new Font("Segoe UI", 8.75F) };
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

            _pnlLlamaContainer.Controls.Add(_lblModel);
            _pnlLlamaContainer.Controls.Add(rowModel);
            _pnlLlamaContainer.Controls.Add(rowParams);
            _pnlLlamaContainer.Controls.Add(_lblUrl);
            _pnlLlamaContainer.Controls.Add(_txtServerUrl);
            _pnlLlamaContainer.Controls.Add(_chkAutoStart);
            _pnlLlamaContainer.Controls.Add(_chkInstantVram);

            // ------------------ 1B. Ollama Container ------------------
            _pnlOllamaContainer = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Visible = false,
                Margin = new Padding(0, 0, 0, 4)
            };

            _lblOllamaInfo = new Label
            {
                Text = "💡 Connects directly to local Ollama. Ensure Ollama is running (`ollama serve` or desktop app).",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.75F, FontStyle.Italic),
                ForeColor = Color.FromArgb(70, 70, 70),
                Margin = new Padding(0, 0, 0, 8)
            };

            _lblOllamaUrl = new Label { Text = "Ollama Endpoint URL:", AutoSize = true, Margin = new Padding(0, 0, 0, 3), Font = new Font("Segoe UI", 8.75F) };
            _txtOllamaUrl = new TextBox { Width = ContentW - 28, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 0, 0, 8), Text = "http://127.0.0.1:11434/v1/chat/completions" };

            _lblOllamaModel = new Label { Text = "Ollama Model Name (e.g. llama3.2, qwen2.5:3b, mistral):", AutoSize = true, Margin = new Padding(0, 0, 0, 3), Font = new Font("Segoe UI", 8.75F) };
            _txtOllamaModel = new TextBox { Width = ContentW - 28, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 0, 0, 6), Text = "llama3.2" };

            // Quick suggestion chips for Ollama
            var rowOllamaChips = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            _lblSuggestions = new Label { Text = "Suggestions:", AutoSize = true, Font = new Font("Segoe UI", 8.25F, FontStyle.Bold), Margin = new Padding(0, 4, 6, 0) };
            rowOllamaChips.Controls.Add(_lblSuggestions);

            string[] ollamaSuggestions = { "llama3.2", "qwen2.5:3b", "mistral", "gemma2:2b", "deepseek-r1:1.5b" };
            foreach (var mod in ollamaSuggestions)
            {
                var btnChip = new Button
                {
                    Text = mod,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(6, 2, 6, 2),
                    Margin = new Padding(0, 0, 4, 2),
                    Font = new Font("Segoe UI", 8F),
                    FlatStyle = FlatStyle.System,
                    Cursor = Cursors.Hand
                };
                string currentMod = mod;
                btnChip.Click += (s, e) => _txtOllamaModel.Text = currentMod;
                rowOllamaChips.Controls.Add(btnChip);
            }

            _pnlOllamaContainer.Controls.Add(_lblOllamaInfo);
            _pnlOllamaContainer.Controls.Add(_lblOllamaUrl);
            _pnlOllamaContainer.Controls.Add(_txtOllamaUrl);
            _pnlOllamaContainer.Controls.Add(_lblOllamaModel);
            _pnlOllamaContainer.Controls.Add(_txtOllamaModel);
            _pnlOllamaContainer.Controls.Add(rowOllamaChips);

            // ------------------ 1C. Cloud / Custom API Container ------------------
            _pnlCloudContainer = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Visible = false,
                Margin = new Padding(0, 0, 0, 4)
            };

            _lblPreset = new Label { Text = "Provider Preset:", AutoSize = true, Margin = new Padding(0, 0, 0, 3), Font = new Font("Segoe UI", 8.75F) };
            _cboCloudPreset = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ContentW - 28,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 0, 8)
            };
            _cboCloudPreset.Items.Add("(Select Provider Preset...)");
            _cboCloudPreset.Items.Add("OpenAI (gpt-4o-mini)");
            _cboCloudPreset.Items.Add("OpenRouter (meta-llama/llama-3.2-3b-instruct:free)");
            _cboCloudPreset.Items.Add("Groq (llama-3.1-8b-instant — Ultra Fast)");
            _cboCloudPreset.Items.Add("DeepSeek (deepseek-chat)");
            _cboCloudPreset.SelectedIndex = 0;
            _cboCloudPreset.SelectedIndexChanged += OnCloudPresetChanged;

            _lblCloudUrl = new Label { Text = "API Endpoint URL (/v1/chat/completions):", AutoSize = true, Margin = new Padding(0, 0, 0, 3), Font = new Font("Segoe UI", 8.75F) };
            _txtCloudUrl = new TextBox { Width = ContentW - 28, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 0, 0, 8), Text = "https://api.openai.com/v1/chat/completions" };

            _lblApiKey = new Label { Text = "API Key (Bearer Token):", AutoSize = true, Margin = new Padding(0, 0, 0, 3), Font = new Font("Segoe UI", 8.75F) };
            
            var rowApiKey = new TableLayoutPanel
            {
                Width = ContentW - 28,
                Height = 32,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 8)
            };
            rowApiKey.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rowApiKey.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _txtCloudApiKey = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F), UseSystemPasswordChar = true, Margin = new Padding(0, 2, 8, 0) };
            _btnToggleKeyVisibility = new Button
            {
                Text = "👁️ Show",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnToggleKeyVisibility.Click += (s, e) =>
            {
                _isApiKeyVisible = !_isApiKeyVisible;
                _txtCloudApiKey.UseSystemPasswordChar = !_isApiKeyVisible;
                _btnToggleKeyVisibility.Text = _isApiKeyVisible ? ("🔒 " + Lang.T(StringKeys.SettingsCloudHide)) : ("👁️ " + Lang.T(StringKeys.SettingsCloudShow));
            };

            rowApiKey.Controls.Add(_txtCloudApiKey, 0, 0);
            rowApiKey.Controls.Add(_btnToggleKeyVisibility, 1, 0);

            _lblCloudModel = new Label { Text = "Model ID / Name (e.g. gpt-4o-mini, deepseek-chat):", AutoSize = true, Margin = new Padding(0, 0, 0, 3), Font = new Font("Segoe UI", 8.75F) };
            _txtCloudModel = new TextBox { Width = ContentW - 28, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 0, 0, 8), Text = "gpt-4o-mini" };

            _pnlCloudContainer.Controls.Add(_lblPreset);
            _pnlCloudContainer.Controls.Add(_cboCloudPreset);
            _pnlCloudContainer.Controls.Add(_lblCloudUrl);
            _pnlCloudContainer.Controls.Add(_txtCloudUrl);
            _pnlCloudContainer.Controls.Add(_lblApiKey);
            _pnlCloudContainer.Controls.Add(rowApiKey);
            _pnlCloudContainer.Controls.Add(_lblCloudModel);
            _pnlCloudContainer.Controls.Add(_txtCloudModel);

            // ------------------ 1D. No AI Custom Settings & Disclaimer ------------------
            _pnlNoAiContainer = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Visible = false,
                Margin = new Padding(0, 0, 0, 8)
            };

            var pnlNoticeBox = new Panel
            {
                Width = ContentW - 28,
                Height = 175,
                BackColor = Color.FromArgb(248, 250, 252),
                Margin = new Padding(0, 4, 0, 8)
            };
            pnlNoticeBox.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(210, 222, 235), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, pnlNoticeBox.Width - 1, pnlNoticeBox.Height - 1);
            };

            _lblNoticeHeader = new Label
            {
                Text = "🚫  No AI Mode (Classic Email Client Mode)",
                Font = new Font("Segoe UI", 9.75F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 45, 65),
                Location = new Point(16, 14),
                AutoSize = true
            };

            _lblNoticeText = new Label
            {
                Text = "Disclaimer & Mode Information:\r\n" +
                       "• Zero AI Overhead: LLM servers, background inference, and GPU/VRAM model loading are completely disabled.\r\n" +
                       "• Full Reading Pane: The AI Summary box is hidden, allowing the email body viewer to expand to 100% height.\r\n" +
                       "• Clean Inbox: The Priority ranking column (⚡) is removed from the inbox list for a clean, classic view.\r\n" +
                       "• Instant Fetching: Emails are synced directly via IMAP without prompt generation or summarization delays.\r\n\r\n" +
                       "Click '💾 Save Settings' at the bottom to apply and save this configuration.",
                Font = new Font("Segoe UI", 8.75F),
                ForeColor = Color.FromArgb(70, 82, 98),
                Location = new Point(16, 40),
                Size = new Size(ContentW - 60, 125),
                AutoSize = false
            };

            pnlNoticeBox.Controls.Add(_lblNoticeHeader);
            pnlNoticeBox.Controls.Add(_lblNoticeText);
            _pnlNoAiContainer.Controls.Add(pnlNoticeBox);

            // ------------------ 1E. Global Inference Settings ------------------
            _pnlGlobalParams = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 4, 0, 8)
            };

            var pnlTemp = new FlowLayoutPanel
            {
                Width = 220,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 0, 20, 0)
            };
            _lblTemp = new Label { Text = "Temperature:", AutoSize = true, Font = new Font("Segoe UI", 8.75F), Margin = new Padding(0, 0, 0, 4) };
            _numTemperature = new NumericUpDown
            {
                Width = 150,
                Height = 28,
                Minimum = 0.0m,
                Maximum = 2.0m,
                DecimalPlaces = 1,
                Increment = 0.1m,
                Value = 0.2m,
                Font = new Font("Segoe UI", 9.5F)
            };
            _lblTempDesc = new Label { Text = "0.0 = Deterministic, 1.0+ = Creative", AutoSize = true, Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(120, 120, 120), Margin = new Padding(0, 2, 0, 0) };
            pnlTemp.Controls.Add(_lblTemp);
            pnlTemp.Controls.Add(_numTemperature);
            pnlTemp.Controls.Add(_lblTempDesc);

            var pnlTokens = new FlowLayoutPanel
            {
                Width = 220,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0)
            };
            _lblTokens = new Label { Text = "Max Response Tokens:", AutoSize = true, Font = new Font("Segoe UI", 8.75F), Margin = new Padding(0, 0, 0, 4) };
            _numMaxTokens = new NumericUpDown
            {
                Width = 150,
                Height = 28,
                Minimum = 64,
                Maximum = 4096,
                Increment = 64,
                Value = 350,
                Font = new Font("Segoe UI", 9.5F)
            };
            _lblTokensDesc = new Label { Text = "Budget for summary length", AutoSize = true, Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(120, 120, 120), Margin = new Padding(0, 2, 0, 0) };
            pnlTokens.Controls.Add(_lblTokens);
            pnlTokens.Controls.Add(_numMaxTokens);
            pnlTokens.Controls.Add(_lblTokensDesc);

            _pnlGlobalParams.Controls.Add(pnlTemp);
            _pnlGlobalParams.Controls.Add(pnlTokens);

            _lblTokenTip = new Label
            {
                Text = "💡 Tip: Lower temperature (0.1 - 0.3) produces consistent, objective executive summaries.",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.FromArgb(90, 110, 140),
                Margin = new Padding(0, 0, 0, 10)
            };

            // ------------------ 1F. Email Ingestion Length Limit ------------------
            _pnlEmailLengthContainer = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 12)
            };

            _lblSummaryCharsHeader = new Label
            {
                Text = "Email Ingestion Character Limit:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 0, 0, 3)
            };

            _lblSummaryCharsDesc = new Label
            {
                Text = "Limits the raw email body length sent to the AI model. Truncation keeps prompts fast and prevents VRAM context overflows.",
                AutoSize = false,
                Width = ContentW - 32,
                Height = 32,
                Font = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Margin = new Padding(0, 0, 0, 6)
            };

            var rowCharControls = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 6)
            };

            _numMaxSummaryChars = new NumericUpDown
            {
                Width = 140,
                Height = 28,
                Minimum = 500,
                Maximum = 500000,
                Increment = 500,
                Value = 4000,
                Font = new Font("Segoe UI", 9.5F)
            };

            _chkUnlimitedEmailChars = new CheckBox
            {
                Text = "Unlimited (Send Full Body)",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(50, 50, 50),
                Margin = new Padding(12, 4, 0, 0)
            };
            _chkUnlimitedEmailChars.CheckedChanged += (s, e) =>
            {
                _numMaxSummaryChars.Enabled = !_chkUnlimitedEmailChars.Checked;
            };

            rowCharControls.Controls.Add(_numMaxSummaryChars);
            rowCharControls.Controls.Add(_chkUnlimitedEmailChars);

            var rowCharPresets = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 4)
            };

            _lblCharPresets = new Label
            {
                Text = "Presets:",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 100, 100),
                Margin = new Padding(0, 5, 6, 0)
            };

            _btnPreset4k = CreateCharLimitPresetChip("4,000 chars (Default)", 4000);
            _btnPreset8k = CreateCharLimitPresetChip("8,000 chars", 8000);
            _btnPreset16k = CreateCharLimitPresetChip("16,000 chars", 16000);
            _btnPreset32k = CreateCharLimitPresetChip("32,000 chars", 32000);
            _btnPresetInf = CreateCharLimitPresetChip("♾️ Unlimited", 0);

            rowCharPresets.Controls.Add(_lblCharPresets);
            rowCharPresets.Controls.Add(_btnPreset4k);
            rowCharPresets.Controls.Add(_btnPreset8k);
            rowCharPresets.Controls.Add(_btnPreset16k);
            rowCharPresets.Controls.Add(_btnPreset32k);
            rowCharPresets.Controls.Add(_btnPresetInf);

            _pnlEmailLengthContainer.Controls.Add(_lblSummaryCharsHeader);
            _pnlEmailLengthContainer.Controls.Add(_lblSummaryCharsDesc);
            _pnlEmailLengthContainer.Controls.Add(rowCharControls);
            _pnlEmailLengthContainer.Controls.Add(rowCharPresets);

            // ------------------ Test Connection Row ------------------
            _rowTestLlm = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 4, 0, 0)
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

            _rowTestLlm.Controls.Add(_btnTestLlm);
            _rowTestLlm.Controls.Add(_lblLlmTestResult);

            pnlLlmCard.Controls.Add(_lblSecLlm);
            pnlLlmCard.Controls.Add(_lblBackend);
            pnlLlmCard.Controls.Add(_barBackendOptions);
            pnlLlmCard.Controls.Add(_pnlBatteryActiveWarning);
            pnlLlmCard.Controls.Add(_cboAiBackend);
            pnlLlmCard.Controls.Add(_pnlLlamaContainer);
            pnlLlmCard.Controls.Add(_pnlOllamaContainer);
            pnlLlmCard.Controls.Add(_pnlCloudContainer);
            pnlLlmCard.Controls.Add(_pnlNoAiContainer);
            pnlLlmCard.Controls.Add(_pnlGlobalParams);
            pnlLlmCard.Controls.Add(_lblTokenTip);
            pnlLlmCard.Controls.Add(_pnlEmailLengthContainer);
            pnlLlmCard.Controls.Add(_rowTestLlm);

            // ==================== 1.5. Battery Saver Section ====================
            var pnlBatteryCard = CreateCardPanel(ContentW);
            
            _lblSecBattery = CreateSectionHeader("🔋  Battery Saver Mode");

            _chkDisableAiOnBattery = new CheckBox
            {
                Text = "Disable AI when on battery power (Auto No AI Mode)",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 0, 0, 4),
                Cursor = Cursors.Hand
            };
            _chkDisableAiOnBattery.CheckedChanged += (s, e) => UpdateBatteryNotice();

            _lblBatteryDesc = new Label
            {
                Text = "When enabled, the app automatically switches to No AI mode whenever your laptop is running on battery power to conserve battery life. Your configured AI backend is preserved and will resume when connected to AC power.",
                AutoSize = false,
                Width = ContentW - 32,
                Height = 34,
                Font = new Font("Segoe UI", 8.25F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Margin = new Padding(0, 0, 0, 4)
            };

            _lblBatteryStatusBadge = new Label
            {
                Text = "",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(80, 90, 100),
                Margin = new Padding(0, 2, 0, 0)
            };

            pnlBatteryCard.Controls.Add(_lblSecBattery);
            pnlBatteryCard.Controls.Add(_chkDisableAiOnBattery);
            pnlBatteryCard.Controls.Add(_lblBatteryDesc);
            pnlBatteryCard.Controls.Add(_lblBatteryStatusBadge);

            // ==================== 2. Email Options Section ====================
            var pnlEmailCard = CreateCardPanel(ContentW);
            
            _lblSecEmail = CreateSectionHeader("📬  Email Fetching Configuration");

            var rowMax = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 6)
            };
            _lblMaxEmails = new Label { Text = "Max Emails per Account:", AutoSize = true, Margin = new Padding(0, 4, 8, 0), Font = new Font("Segoe UI", 9F) };
            _numMaxEmails = new NumericUpDown { Width = 75, Minimum = 1, Maximum = 100, Value = 15, Font = new Font("Segoe UI", 9F) };
            rowMax.Controls.Add(_lblMaxEmails);
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
                Margin = new Padding(0, 0, 0, 6),
                Font = new Font("Segoe UI", 9F)
            };

            var rowPreview = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 4, 0, 2)
            };
            _lblPreview = new Label { Text = "Multi-select preview email (Ctrl+click):", AutoSize = true, Margin = new Padding(0, 4, 8, 0), Font = new Font("Segoe UI", 9F) };
            _cboMultiSelectPreview = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 240,
                Height = 28,
                Font = new Font("Segoe UI", 9F)
            };
            _cboMultiSelectPreview.Items.Add("Show last selected email (Default)");
            _cboMultiSelectPreview.Items.Add("Show first selected email");
            _cboMultiSelectPreview.SelectedIndex = 0;
            rowPreview.Controls.Add(_lblPreview);
            rowPreview.Controls.Add(_cboMultiSelectPreview);

            pnlEmailCard.Controls.Add(_lblSecEmail);
            pnlEmailCard.Controls.Add(rowMax);
            pnlEmailCard.Controls.Add(_chkOnlyUnread);
            pnlEmailCard.Controls.Add(_chkMarkAsSeen);
            pnlEmailCard.Controls.Add(rowPreview);

            // ==================== 2B. Attachment Downloads Section ====================
            var pnlAttachmentCard = CreateCardPanel(ContentW);
            _lblSecAttachments = CreateSectionHeader("📁  Attachment Downloads");

            _lblDownloadPathHeader = new Label
            {
                Text = "Default Download Folder:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 0, 0, 2)
            };

            _lblDownloadPathDesc = new Label
            {
                Text = "Location where email attachments will be saved by default (defaults to your Windows Downloads folder).",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Margin = new Padding(0, 0, 0, 8)
            };

            var rowPathControls = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 2, 0, 6)
            };

            _txtAttachmentDownloadPath = new TextBox
            {
                Width = 340,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 2, 8, 4)
            };

            _btnBrowsePath = new Button
            {
                Text = "Browse...",
                Width = 85,
                Height = 26,
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(0, 1, 6, 4)
            };
            _btnBrowsePath.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog
                {
                    Description = "Select Default Attachment Download Folder",
                    UseDescriptionForTitle = true,
                    SelectedPath = Directory.Exists(_txtAttachmentDownloadPath.Text.Trim()) 
                        ? _txtAttachmentDownloadPath.Text.Trim() 
                        : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads"
                };
                if (fbd.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    _txtAttachmentDownloadPath.Text = fbd.SelectedPath;
                }
            };

            _btnResetPath = new Button
            {
                Text = "Default",
                Width = 75,
                Height = 26,
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(0, 1, 0, 4)
            };
            _btnResetPath.Click += (s, e) =>
            {
                _txtAttachmentDownloadPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            };

            rowPathControls.Controls.Add(_txtAttachmentDownloadPath);
            rowPathControls.Controls.Add(_btnBrowsePath);
            rowPathControls.Controls.Add(_btnResetPath);

            pnlAttachmentCard.Controls.Add(_lblSecAttachments);
            pnlAttachmentCard.Controls.Add(_lblDownloadPathHeader);
            pnlAttachmentCard.Controls.Add(_lblDownloadPathDesc);
            pnlAttachmentCard.Controls.Add(rowPathControls);

            // ==================== 2.5. Language & Region Section ====================
            var pnlLangCard = CreateCardPanel(ContentW);
            _lblSecLanguage = CreateSectionHeader(Lang.T(StringKeys.SettingsSecLanguage));

            _lblLanguageDesc = new Label
            {
                Text = Lang.T(StringKeys.SettingsLanguageDesc),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Margin = new Padding(0, 0, 0, 8)
            };

            _cboLanguage = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 280,
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 0, 0, 4)
            };

            PopulateLanguageDropdown();

            _cboLanguage.SelectedIndexChanged += (s, e) =>
            {
                if (_cboLanguage.SelectedItem is LanguageComboItem item)
                {
                    LanguageManager.Instance.SetLanguage(item.Language.Code);
                }
            };

            pnlLangCard.Controls.Add(_lblSecLanguage);
            pnlLangCard.Controls.Add(_lblLanguageDesc);
            pnlLangCard.Controls.Add(_cboLanguage);

            // ==================== 3. Interface & Layout Section ====================
            var pnlUiCard = CreateCardPanel(ContentW);
            _lblSecUi = CreateSectionHeader("🖥️  Interface & Layout");

            _chkCollapseSidebarByDefault = new CheckBox
            {
                Text = "Start with left sidebar collapsed by default (compact icon rail on launch)",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(0, 0, 0, 10),
                Font = new Font("Segoe UI", 9F)
            };

            _lblScalingHeader = new Label
            {
                Text = "Default Launch Window Scaling (Relative to Display):",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 4, 0, 2)
            };

            _lblScalingDesc = new Label
            {
                Text = "Target proportion of the active monitor's usable desktop area (working area) on launch (Default: 60% width × 56% height).",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Margin = new Padding(0, 0, 0, 8)
            };

            var rowScaleControls = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 6)
            };

            _lblWidth = new Label { Text = "Width Scale (%):", AutoSize = true, Margin = new Padding(0, 4, 6, 0), Font = new Font("Segoe UI", 9F) };
            _numWindowWidthScale = new NumericUpDown
            {
                Width = 80,
                DecimalPlaces = 1,
                Minimum = 30.0M,
                Maximum = 100.0M,
                Increment = 1.0M,
                Value = 60.0M,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 16, 0)
            };
            _numWindowWidthScale.ValueChanged += (s, e) => UpdateScalePreview();

            _lblHeight = new Label { Text = "Height Scale (%):", AutoSize = true, Margin = new Padding(0, 4, 6, 0), Font = new Font("Segoe UI", 9F) };
            _numWindowHeightScale = new NumericUpDown
            {
                Width = 80,
                DecimalPlaces = 1,
                Minimum = 30.0M,
                Maximum = 100.0M,
                Increment = 0.5M,
                Value = 56.0M,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0, 0, 14, 0)
            };
            _numWindowHeightScale.ValueChanged += (s, e) => UpdateScalePreview();

            _btnApplyWindowSizeNow = new Button
            {
                Text = "⚡ Resize Active Window",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0, 0, 0, 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnApplyWindowSizeNow.Click += OnApplyWindowSizeNowClick;

            rowScaleControls.Controls.Add(_lblWidth);
            rowScaleControls.Controls.Add(_numWindowWidthScale);
            rowScaleControls.Controls.Add(_lblHeight);
            rowScaleControls.Controls.Add(_numWindowHeightScale);
            rowScaleControls.Controls.Add(_btnApplyWindowSizeNow);

            var rowPresets = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 6)
            };

            _lblLayoutPresets = new Label { Text = "Presets:", AutoSize = true, Margin = new Padding(0, 4, 6, 0), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(80, 80, 80) };
            _btnPresetDefault = CreatePresetChip("60% × 56% (Default)", 60.0M, 56.0M);
            _btnPresetCompact = CreatePresetChip("50% × 50% (Compact)", 50.0M, 50.0M);
            _btnPresetLarge = CreatePresetChip("75% × 70% (Large)", 75.0M, 70.0M);
            _btnPresetMax = CreatePresetChip("95% × 90% (Near Max)", 95.0M, 90.0M);

            rowPresets.Controls.Add(_lblLayoutPresets);
            rowPresets.Controls.Add(_btnPresetDefault);
            rowPresets.Controls.Add(_btnPresetCompact);
            rowPresets.Controls.Add(_btnPresetLarge);
            rowPresets.Controls.Add(_btnPresetMax);

            _lblScalePreview = new Label
            {
                Text = "",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(70, 70, 70),
                Margin = new Padding(0, 2, 0, 2)
            };

            _btnCreateShortcuts = new Button
            {
                Text = "📌  Add Desktop & Start Menu Shortcuts",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 5, 10, 5),
                Margin = new Padding(0, 8, 0, 2),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnCreateShortcuts.Click += (s, e) =>
            {
                bool ok = ShortcutService.CreateShortcuts();
                if (ok)
                {
                    MessageBox.Show(Lang.T(StringKeys.SettingsShortcutsSuccess), Lang.T(StringKeys.CommonSuccess), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(Lang.T(StringKeys.SettingsShortcutsError), Lang.T(StringKeys.CommonWarning), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            pnlUiCard.Controls.Add(_lblSecUi);
            pnlUiCard.Controls.Add(_chkCollapseSidebarByDefault);
            pnlUiCard.Controls.Add(_lblScalingHeader);
            pnlUiCard.Controls.Add(_lblScalingDesc);
            pnlUiCard.Controls.Add(rowScaleControls);
            pnlUiCard.Controls.Add(rowPresets);
            pnlUiCard.Controls.Add(_lblScalePreview);
            pnlUiCard.Controls.Add(_btnCreateShortcuts);

            // ==================== 4. System Tray Daemon & Notifications ====================
            var pnlTrayCard = CreateCardPanel(ContentW);
            _lblSecTray = CreateSectionHeader("🔔  System Tray Daemon & Notifications");

            _chkAlwaysKeepOn = new CheckBox
            {
                Text = "Always keep on (Run system tray daemon in background)",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 0, 0, 4),
                Font = new Font("Segoe UI", 9F)
            };

            _chkEnableTrayNotifs = new CheckBox
            {
                Text = "Enable Windows desktop notifications",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 0, 0, 6),
                Font = new Font("Segoe UI", 9F)
            };

            var rowTrayInterval = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 6)
            };
            _lblInterval = new Label { Text = "Check interval (minutes):", AutoSize = true, Margin = new Padding(0, 4, 8, 0), Font = new Font("Segoe UI", 9F) };
            _numTrayInterval = new NumericUpDown { Width = 75, Minimum = 1, Maximum = 120, Value = 5, Font = new Font("Segoe UI", 9F) };
            rowTrayInterval.Controls.Add(_lblInterval);
            rowTrayInterval.Controls.Add(_numTrayInterval);

            _chkStartWithWindows = new CheckBox
            {
                Text = "Start system tray daemon on user log-in",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(0, 0, 0, 8),
                Font = new Font("Segoe UI", 9F)
            };

            var rowDaemonAction = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 2, 0, 0)
            };

            _btnRestartDaemon = new Button
            {
                Text = "🔄  Restart / Start Tray Daemon",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 5, 12, 5),
                Margin = new Padding(0, 0, 10, 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnRestartDaemon.Click += OnRestartDaemonClick;

            _lblDaemonStatus = new Label
            {
                Text = "",
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 0),
                Font = new Font("Segoe UI", 8.75F)
            };

            rowDaemonAction.Controls.Add(_btnRestartDaemon);
            rowDaemonAction.Controls.Add(_lblDaemonStatus);

            pnlTrayCard.Controls.Add(_lblSecTray);
            pnlTrayCard.Controls.Add(_chkAlwaysKeepOn);
            pnlTrayCard.Controls.Add(_chkEnableTrayNotifs);
            pnlTrayCard.Controls.Add(rowTrayInterval);
            pnlTrayCard.Controls.Add(_chkStartWithWindows);
            pnlTrayCard.Controls.Add(rowDaemonAction);

            // ==================== 4. Prompt Template Section ====================
            var pnlPromptCard = CreateCardPanel(ContentW);
            
            _lblSecPrompt = CreateSectionHeader("✍️  AI System Prompt Template");

            _txtPrompt = new TextBox
            {
                Width = ContentW - 28,
                Height = 120,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9F),
                Margin = new Padding(0)
            };

            pnlPromptCard.Controls.Add(_lblSecPrompt);
            pnlPromptCard.Controls.Add(_txtPrompt);

            // ==================== 5. Bottom Action Buttons ====================
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
            _mainFlow.Controls.Add(pnlBatteryCard);
            _mainFlow.Controls.Add(pnlLangCard);
            _mainFlow.Controls.Add(pnlEmailCard);
            _mainFlow.Controls.Add(pnlAttachmentCard);
            _mainFlow.Controls.Add(pnlUiCard);
            _mainFlow.Controls.Add(pnlTrayCard);
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

        private Button CreateBackendOptionButton(string title, int index)
        {
            var btn = new Button
            {
                Text = title,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, 36),
                Padding = new Padding(16, 7, 16, 7),
                Margin = new Padding(0, 0, 8, 4),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.Click += (s, e) =>
            {
                _cboAiBackend.SelectedIndex = index;
            };
            return btn;
        }

        private static void UpdateButtonState(Button? btn, bool isSelected)
        {
            if (btn == null) return;
            if (isSelected)
            {
                btn.BackColor = Color.FromArgb(0, 102, 204);
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = Color.FromArgb(0, 90, 180);
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 115, 230);
            }
            else
            {
                btn.BackColor = Color.FromArgb(245, 247, 250);
                btn.ForeColor = Color.FromArgb(45, 55, 72);
                btn.FlatAppearance.BorderColor = Color.FromArgb(215, 225, 235);
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 242, 250);
            }
        }

        private void UpdateAiBackendPanelsVisibility()
        {
            int idx = _cboAiBackend.SelectedIndex;

            UpdateButtonState(_btnBackendLlama, idx == 0);
            UpdateButtonState(_btnBackendOllama, idx == 1);
            UpdateButtonState(_btnBackendCloud, idx == 2);
            UpdateButtonState(_btnBackendNoAi, idx == 3);

            _pnlLlamaContainer.Visible = (idx == 0);
            _pnlOllamaContainer.Visible = (idx == 1);
            _pnlCloudContainer.Visible = (idx == 2);
            _pnlNoAiContainer.Visible = (idx == 3);
            _pnlGlobalParams.Visible = (idx != 3);
            _lblTokenTip.Visible = (idx != 3);
            _pnlEmailLengthContainer.Visible = (idx != 3);
            _rowTestLlm.Visible = (idx != 3);
            _lblLlmTestResult.Text = "";

            UpdateBatteryNotice();
        }

        public void UpdateBatteryNotice()
        {
            if (this.IsDisposed) return;

            bool onBattery = AppSettings.IsRunningOnBattery();
            bool disableOnBat = _chkDisableAiOnBattery?.Checked ?? _configService.Settings.DisableAiOnBattery;
            bool isBatterySaverActive = disableOnBat && onBattery;

            if (_lblBatteryStatusBadge != null)
            {
                if (onBattery)
                {
                    _lblBatteryStatusBadge.Text = isBatterySaverActive 
                        ? $"🔋 {Lang.T(StringKeys.SettingsBatteryActive)}" 
                        : $"🔋 {Lang.T(StringKeys.SettingsBatteryActive)}";
                    _lblBatteryStatusBadge.ForeColor = isBatterySaverActive ? Color.FromArgb(180, 100, 0) : Color.FromArgb(90, 90, 90);
                }
                else
                {
                    _lblBatteryStatusBadge.Text = $"🔌 {Lang.T(StringKeys.SettingsBatteryAc)}";
                    _lblBatteryStatusBadge.ForeColor = Color.FromArgb(40, 130, 40);
                }
            }

            if (_pnlBatteryActiveWarning != null)
            {
                int idx = _cboAiBackend.SelectedIndex;
                _pnlBatteryActiveWarning.Visible = isBatterySaverActive && idx != 3;
            }
        }

        private void OnCloudPresetChanged(object? sender, EventArgs e)
        {
            int idx = _cboCloudPreset.SelectedIndex;
            if (idx == 1) // OpenAI
            {
                _txtCloudUrl.Text = "https://api.openai.com/v1/chat/completions";
                _txtCloudModel.Text = "gpt-4o-mini";
            }
            else if (idx == 2) // OpenRouter
            {
                _txtCloudUrl.Text = "https://openrouter.ai/api/v1/chat/completions";
                _txtCloudModel.Text = "meta-llama/llama-3.2-3b-instruct:free";
            }
            else if (idx == 3) // Groq
            {
                _txtCloudUrl.Text = "https://api.groq.com/openai/v1/chat/completions";
                _txtCloudModel.Text = "llama-3.1-8b-instant";
            }
            else if (idx == 4) // DeepSeek
            {
                _txtCloudUrl.Text = "https://api.deepseek.com/v1/chat/completions";
                _txtCloudModel.Text = "deepseek-chat";
            }
        }

        public void LoadSettings()
        {
            var s = _configService.Settings;

            // AI Backend Selection (preserving user's configured backend even if currently on battery)
            if (s.IsExplicitAiDisabled)
            {
                _cboAiBackend.SelectedIndex = 3;
            }
            else if (string.Equals(s.AiBackend, "Ollama", StringComparison.OrdinalIgnoreCase))
            {
                _cboAiBackend.SelectedIndex = 1;
            }
            else if (string.Equals(s.AiBackend, "Cloud", StringComparison.OrdinalIgnoreCase))
            {
                _cboAiBackend.SelectedIndex = 2;
            }
            else
            {
                _cboAiBackend.SelectedIndex = 0;
            }
            UpdateAiBackendPanelsVisibility();

            _chkDisableAiOnBattery.Checked = s.DisableAiOnBattery;
            UpdateBatteryNotice();

            // llama.cpp settings
            _txtModelPath.Text = s.LlamaModelPath;
            _numPort.Value = Math.Max(_numPort.Minimum, Math.Min(_numPort.Maximum, s.LlamaServerPort));
            _numGpuLayers.Value = Math.Max(_numGpuLayers.Minimum, Math.Min(_numGpuLayers.Maximum, s.LlamaGpuLayers));
            _numContextSize.Value = Math.Max(_numContextSize.Minimum, Math.Min(_numContextSize.Maximum, s.LlamaContextSize > 0 ? s.LlamaContextSize : 8192));
            _txtServerUrl.Text = s.LlamaServerUrl;
            _chkAutoStart.Checked = s.AutoStartLlamaServer;
            _chkInstantVram.Checked = s.InstantVramUnload;

            // Ollama settings
            _txtOllamaUrl.Text = s.OllamaServerUrl;
            _txtOllamaModel.Text = s.OllamaModelName;

            // Cloud settings
            _txtCloudUrl.Text = s.CloudApiUrl;
            _txtCloudApiKey.Text = s.CloudApiKey;
            _txtCloudModel.Text = s.CloudModelName;

            // Global LLM settings
            _numTemperature.Value = Math.Max(_numTemperature.Minimum, Math.Min(_numTemperature.Maximum, (decimal)s.Temperature));
            _numMaxTokens.Value = Math.Max(_numMaxTokens.Minimum, Math.Min(_numMaxTokens.Maximum, s.MaxTokens > 0 ? s.MaxTokens : 350));
            if (s.MaxSummaryEmailChars <= 0)
            {
                _chkUnlimitedEmailChars.Checked = true;
                _numMaxSummaryChars.Value = 4000;
                _numMaxSummaryChars.Enabled = false;
            }
            else
            {
                _chkUnlimitedEmailChars.Checked = false;
                _numMaxSummaryChars.Value = Math.Max(_numMaxSummaryChars.Minimum, Math.Min(_numMaxSummaryChars.Maximum, s.MaxSummaryEmailChars));
                _numMaxSummaryChars.Enabled = true;
            }

            // Email settings
            _numMaxEmails.Value = Math.Max(_numMaxEmails.Minimum, Math.Min(_numMaxEmails.Maximum, s.MaxEmailsPerAccount));
            _chkOnlyUnread.Checked = s.OnlyUnread;
            _chkMarkAsSeen.Checked = s.MarkAsSeen;
            _cboMultiSelectPreview.SelectedIndex = string.Equals(s.MultiSelectPreview, "FirstSelected", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            _txtAttachmentDownloadPath.Text = s.GetEffectiveAttachmentDownloadPath();

            // UI & Layout settings
            _chkCollapseSidebarByDefault.Checked = s.CollapseSidebarByDefault;
            decimal wScale = (decimal)(s.WindowWidthScale > 0.1 && s.WindowWidthScale <= 1.0 ? s.WindowWidthScale * 100.0 : 60.0);
            decimal hScale = (decimal)(s.WindowHeightScale > 0.1 && s.WindowHeightScale <= 1.0 ? s.WindowHeightScale * 100.0 : 56.0);
            _numWindowWidthScale.Value = Math.Max(_numWindowWidthScale.Minimum, Math.Min(_numWindowWidthScale.Maximum, wScale));
            _numWindowHeightScale.Value = Math.Max(_numWindowHeightScale.Minimum, Math.Min(_numWindowHeightScale.Maximum, hScale));
            UpdateScalePreview();

            // System Tray settings
            _chkAlwaysKeepOn.Checked = s.AlwaysKeepOn;
            _chkEnableTrayNotifs.Checked = s.EnableTrayNotifications;
            _numTrayInterval.Value = Math.Max(_numTrayInterval.Minimum, Math.Min(_numTrayInterval.Maximum, s.TrayRefreshIntervalMinutes));
            _chkStartWithWindows.Checked = s.StartWithWindows || IsStartupWithWindowsEnabled();

            // Language setting
            string curLang = s.Language ?? "en";
            for (int i = 0; i < _cboLanguage.Items.Count; i++)
            {
                if (_cboLanguage.Items[i] is LanguageComboItem item && item.Language.Code.Equals(curLang, StringComparison.OrdinalIgnoreCase))
                {
                    _cboLanguage.SelectedIndex = i;
                    break;
                }
            }
            if (_cboLanguage.SelectedIndex < 0 && _cboLanguage.Items.Count > 0)
            {
                _cboLanguage.SelectedIndex = 0;
            }

            _txtPrompt.Text = s.SystemPrompt;
        }

        private void PopulateLanguageDropdown()
        {
            _cboLanguage.Items.Clear();
            foreach (var lang in LanguageManager.Instance.AvailableLanguages)
            {
                _cboLanguage.Items.Add(new LanguageComboItem(lang));
            }
        }

        public void ApplyLocalization()
        {
            if (this.IsDisposed) return;

            // 1. AI Engine Section
            if (_lblSecLlm != null) _lblSecLlm.Text = Lang.T(StringKeys.SettingsSecAiBackend);
            if (_lblBackend != null) _lblBackend.Text = Lang.T(StringKeys.SettingsBackendSelect);
            if (_btnBackendLlama != null) _btnBackendLlama.Text = Lang.T(StringKeys.SettingsBackendLlama);
            if (_btnBackendOllama != null) _btnBackendOllama.Text = Lang.T(StringKeys.SettingsBackendOllama);
            if (_btnBackendCloud != null) _btnBackendCloud.Text = Lang.T(StringKeys.SettingsBackendCloud);
            if (_btnBackendNoAi != null) _btnBackendNoAi.Text = Lang.T(StringKeys.SettingsBackendNoAi);
            if (_lblWarnTitle != null) _lblWarnTitle.Text = Lang.T(StringKeys.SettingsBatteryWarningTitle);
            if (_lblWarnDesc != null) _lblWarnDesc.Text = Lang.T(StringKeys.SettingsBatteryWarningDesc);
            if (_lblModel != null) _lblModel.Text = Lang.T(StringKeys.SettingsLlamaModelPath);
            if (_btnBrowseModel != null) _btnBrowseModel.Text = Lang.T(StringKeys.SettingsBrowse);
            if (_lblLayers != null) _lblLayers.Text = Lang.T(StringKeys.SettingsLlamaLayers);
            if (_lblPort != null) _lblPort.Text = Lang.T(StringKeys.SettingsLlamaPort);
            if (_lblContextSize != null) _lblContextSize.Text = Lang.T(StringKeys.SettingsLlamaContext);
            if (_lblUrl != null) _lblUrl.Text = Lang.T(StringKeys.SettingsLlamaUrl);
            if (_chkAutoStart != null) _chkAutoStart.Text = Lang.T(StringKeys.SettingsLlamaAutoStart);
            if (_chkInstantVram != null) _chkInstantVram.Text = Lang.T(StringKeys.SettingsLlamaInstantVram);

            // 1B. Ollama
            if (_lblOllamaInfo != null) _lblOllamaInfo.Text = Lang.T(StringKeys.SettingsOllamaInfo);
            if (_lblOllamaUrl != null) _lblOllamaUrl.Text = Lang.T(StringKeys.SettingsOllamaUrl);
            if (_lblOllamaModel != null) _lblOllamaModel.Text = Lang.T(StringKeys.SettingsOllamaModel);
            if (_lblSuggestions != null) _lblSuggestions.Text = Lang.T(StringKeys.SettingsSuggestions);

            // 1C. Cloud
            if (_lblPreset != null) _lblPreset.Text = Lang.T(StringKeys.SettingsCloudPreset);
            if (_cboCloudPreset != null && _cboCloudPreset.Items.Count > 0)
            {
                _cboCloudPreset.Items[0] = Lang.T(StringKeys.SettingsCloudPresetSelect);
            }
            if (_lblCloudUrl != null) _lblCloudUrl.Text = Lang.T(StringKeys.SettingsCloudUrl);
            if (_lblApiKey != null) _lblApiKey.Text = Lang.T(StringKeys.SettingsCloudKey);
            if (_btnToggleKeyVisibility != null) _btnToggleKeyVisibility.Text = _isApiKeyVisible ? ("🔒 " + Lang.T(StringKeys.SettingsCloudHide)) : ("👁️ " + Lang.T(StringKeys.SettingsCloudShow));
            if (_lblCloudModel != null) _lblCloudModel.Text = Lang.T(StringKeys.SettingsCloudModel);

            // 1D. No AI
            if (_lblNoticeHeader != null) _lblNoticeHeader.Text = Lang.T(StringKeys.SettingsNoAiTitle);
            if (_lblNoticeText != null) _lblNoticeText.Text = Lang.T(StringKeys.SettingsNoAiDisclaimer);

            // 1E. Global Params
            if (_lblTemp != null) _lblTemp.Text = Lang.T(StringKeys.SettingsTemp);
            if (_lblTempDesc != null) _lblTempDesc.Text = Lang.T(StringKeys.SettingsTempDesc);
            if (_lblTokens != null) _lblTokens.Text = Lang.T(StringKeys.SettingsMaxTokens);
            if (_lblTokensDesc != null) _lblTokensDesc.Text = Lang.T(StringKeys.SettingsMaxTokensDesc);
            if (_lblTokenTip != null) _lblTokenTip.Text = Lang.T(StringKeys.SettingsTokenTip);

            // 1F. Ingestion Limit
            if (_lblSummaryCharsHeader != null) _lblSummaryCharsHeader.Text = Lang.T(StringKeys.SettingsEmailLimitHeader);
            if (_lblSummaryCharsDesc != null) _lblSummaryCharsDesc.Text = Lang.T(StringKeys.SettingsEmailLimitDesc);
            if (_chkUnlimitedEmailChars != null) _chkUnlimitedEmailChars.Text = Lang.T(StringKeys.SettingsUnlimited);
            if (_lblCharPresets != null) _lblCharPresets.Text = Lang.T(StringKeys.SettingsPresets);
            if (_btnPreset4k != null) _btnPreset4k.Text = Lang.T(StringKeys.SettingsCharsDefault);
            if (_btnPreset8k != null) _btnPreset8k.Text = Lang.T(StringKeys.SettingsChars8k);
            if (_btnPreset16k != null) _btnPreset16k.Text = Lang.T(StringKeys.SettingsChars16k);
            if (_btnPreset32k != null) _btnPreset32k.Text = Lang.T(StringKeys.SettingsChars32k);
            if (_btnPresetInf != null) _btnPresetInf.Text = Lang.T(StringKeys.SettingsCharsUnlimited);

            // Test Connection
            if (_btnTestLlm != null) _btnTestLlm.Text = "⚡ " + Lang.T(StringKeys.SettingsBtnTestLlm);

            // 1.5. Battery
            if (_lblSecBattery != null) _lblSecBattery.Text = Lang.T(StringKeys.SettingsSecBattery);
            if (_chkDisableAiOnBattery != null) _chkDisableAiOnBattery.Text = Lang.T(StringKeys.SettingsBatteryDisableAi);
            if (_lblBatteryDesc != null) _lblBatteryDesc.Text = Lang.T(StringKeys.SettingsBatteryDesc);
            UpdateBatteryNotice();

            // 2. Language
            if (_lblSecLanguage != null) _lblSecLanguage.Text = Lang.T(StringKeys.SettingsSecLanguage);
            if (_lblLanguageDesc != null) _lblLanguageDesc.Text = Lang.T(StringKeys.SettingsLanguageDesc);

            // 2. Email Options
            if (_lblSecEmail != null) _lblSecEmail.Text = Lang.T(StringKeys.SettingsSecEmail);
            if (_lblMaxEmails != null) _lblMaxEmails.Text = Lang.T(StringKeys.SettingsMaxEmails);
            if (_chkOnlyUnread != null) _chkOnlyUnread.Text = Lang.T(StringKeys.SettingsOnlyUnread);
            if (_chkMarkAsSeen != null) _chkMarkAsSeen.Text = Lang.T(StringKeys.SettingsMarkAsSeen);
            if (_lblPreview != null) _lblPreview.Text = Lang.T(StringKeys.SettingsMultiSelectPreview);
            if (_cboMultiSelectPreview != null && _cboMultiSelectPreview.Items.Count >= 2)
            {
                int prevSel = _cboMultiSelectPreview.SelectedIndex;
                _cboMultiSelectPreview.Items[0] = Lang.T(StringKeys.SettingsMultiSelectLast);
                _cboMultiSelectPreview.Items[1] = Lang.T(StringKeys.SettingsMultiSelectFirst);
                _cboMultiSelectPreview.SelectedIndex = prevSel >= 0 ? prevSel : 0;
            }

            // 2B. Attachments
            if (_lblSecAttachments != null) _lblSecAttachments.Text = Lang.T(StringKeys.SettingsSecAttachments);
            if (_lblDownloadPathHeader != null) _lblDownloadPathHeader.Text = Lang.T(StringKeys.SettingsDownloadPathHeader);
            if (_lblDownloadPathDesc != null) _lblDownloadPathDesc.Text = Lang.T(StringKeys.SettingsDownloadPathDesc);
            if (_btnBrowsePath != null) _btnBrowsePath.Text = Lang.T(StringKeys.SettingsBrowse);
            if (_btnResetPath != null) _btnResetPath.Text = Lang.T(StringKeys.SettingsDefault);

            // 3. UI & Layout
            if (_lblSecUi != null) _lblSecUi.Text = Lang.T(StringKeys.SettingsSecUi);
            if (_chkCollapseSidebarByDefault != null) _chkCollapseSidebarByDefault.Text = Lang.T(StringKeys.SettingsCollapseSidebar);
            if (_lblScalingHeader != null) _lblScalingHeader.Text = Lang.T(StringKeys.SettingsScalingHeader);
            if (_lblScalingDesc != null) _lblScalingDesc.Text = Lang.T(StringKeys.SettingsScalingDesc);
            if (_lblWidth != null) _lblWidth.Text = Lang.T(StringKeys.SettingsWidthScale);
            if (_lblHeight != null) _lblHeight.Text = Lang.T(StringKeys.SettingsHeightScale);
            if (_btnApplyWindowSizeNow != null) _btnApplyWindowSizeNow.Text = "⚡ " + Lang.T(StringKeys.SettingsResizeActive);
            if (_lblLayoutPresets != null) _lblLayoutPresets.Text = Lang.T(StringKeys.SettingsPresets);
            if (_btnPresetDefault != null) _btnPresetDefault.Text = Lang.T(StringKeys.SettingsPresetDefault);
            if (_btnPresetCompact != null) _btnPresetCompact.Text = Lang.T(StringKeys.SettingsPresetCompact);
            if (_btnPresetLarge != null) _btnPresetLarge.Text = Lang.T(StringKeys.SettingsPresetLarge);
            if (_btnPresetMax != null) _btnPresetMax.Text = Lang.T(StringKeys.SettingsPresetMax);
            if (_btnCreateShortcuts != null) _btnCreateShortcuts.Text = "📌  " + Lang.T(StringKeys.SettingsAddShortcuts);
            UpdateScalePreview();

            // 4. System Tray
            if (_lblSecTray != null) _lblSecTray.Text = Lang.T(StringKeys.SettingsSecTray);
            if (_chkAlwaysKeepOn != null) _chkAlwaysKeepOn.Text = Lang.T(StringKeys.SettingsAlwaysKeepOn);
            if (_chkEnableTrayNotifs != null) _chkEnableTrayNotifs.Text = Lang.T(StringKeys.SettingsEnableTrayNotifs);
            if (_lblInterval != null) _lblInterval.Text = Lang.T(StringKeys.SettingsCheckInterval);
            if (_chkStartWithWindows != null) _chkStartWithWindows.Text = Lang.T(StringKeys.SettingsStartWithWindows);
            if (_btnRestartDaemon != null) _btnRestartDaemon.Text = "🔄  " + Lang.T(StringKeys.SettingsRestartDaemon);

            // 5. Prompt & Bottom Buttons
            if (_lblSecPrompt != null) _lblSecPrompt.Text = Lang.T(StringKeys.SettingsSecPrompt);
            if (_btnSave != null) _btnSave.Text = "💾 " + Lang.T(StringKeys.SettingsBtnSave);
            if (_btnReset != null) _btnReset.Text = "↺ " + Lang.T(StringKeys.SettingsBtnReset);
        }

        private class LanguageComboItem
        {
            public ILanguage Language { get; }
            public LanguageComboItem(ILanguage lang) => Language = lang;
            public override string ToString() => $"{Language.FlagEmoji}  {Language.Name} ({Language.Code})";
        }

        private Button CreateCharLimitPresetChip(string text, int charLimit)
        {
            var btn = new Button
            {
                Text = text,
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(6, 2, 6, 2),
                Margin = new Padding(0, 0, 6, 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.25F)
            };
            btn.Click += (s, e) =>
            {
                if (charLimit <= 0)
                {
                    _chkUnlimitedEmailChars.Checked = true;
                }
                else
                {
                    _chkUnlimitedEmailChars.Checked = false;
                    _numMaxSummaryChars.Value = Math.Max(_numMaxSummaryChars.Minimum, Math.Min(_numMaxSummaryChars.Maximum, charLimit));
                }
            };
            return btn;
        }

        private Button CreatePresetChip(string text, decimal widthVal, decimal heightVal)
        {
            var btn = new Button
            {
                Text = text,
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(6, 2, 6, 2),
                Margin = new Padding(0, 0, 6, 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.25F)
            };
            btn.Click += (s, e) =>
            {
                _numWindowWidthScale.Value = widthVal;
                _numWindowHeightScale.Value = heightVal;
            };
            return btn;
        }

        private void UpdateScalePreview()
        {
            try
            {
                if (_lblScalePreview == null || _numWindowWidthScale == null || _numWindowHeightScale == null) return;
                var screen = Screen.FromControl(this) ?? Screen.PrimaryScreen;
                var wa = screen?.WorkingArea ?? (Screen.PrimaryScreen != null ? Screen.PrimaryScreen.WorkingArea : new Rectangle(0, 0, 1920, 1080));
                double wScale = (double)_numWindowWidthScale.Value / 100.0;
                double hScale = (double)_numWindowHeightScale.Value / 100.0;
                int targetW = (int)Math.Round(wa.Width * wScale);
                int targetH = (int)Math.Round(wa.Height * hScale);
                _lblScalePreview.Text = Lang.Format(StringKeys.SettingsLaunchDimensions, targetW, targetH, wa.Width, wa.Height);
            }
            catch
            {
                // Fallback
            }
        }

        private void OnApplyWindowSizeNowClick(object? sender, EventArgs e)
        {
            try
            {
                var mainForm = this.FindForm();
                if (mainForm != null)
                {
                    var screen = Screen.FromControl(mainForm) ?? Screen.PrimaryScreen;
                    var wa = screen?.WorkingArea ?? (Screen.PrimaryScreen != null ? Screen.PrimaryScreen.WorkingArea : new Rectangle(0, 0, 1920, 1080));
                    double wScale = (double)_numWindowWidthScale.Value / 100.0;
                    double hScale = (double)_numWindowHeightScale.Value / 100.0;
                    int targetW = (int)Math.Round(wa.Width * wScale);
                    int targetH = (int)Math.Round(wa.Height * hScale);
                    int minW = Math.Min(960, wa.Width);
                    int minH = Math.Min(540, wa.Height);
                    targetW = Math.Clamp(targetW, minW, wa.Width);
                    targetH = Math.Clamp(targetH, minH, wa.Height);
                    mainForm.Size = new Size(targetW, targetH);
                    mainForm.Location = new Point(
                        wa.Left + Math.Max(0, (wa.Width - targetW) / 2),
                        wa.Top + Math.Max(0, (wa.Height - targetH) / 2)
                    );
                    _configService.Settings.WindowWidth = targetW;
                    _configService.Settings.WindowHeight = targetH;
                    _configService.Settings.WindowWidthScale = wScale;
                    _configService.Settings.WindowHeightScale = hScale;
                    _configService.SaveConfig();
                }
            }
            catch { }
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

            string url;
            string model;
            string? apiKey = null;

            if (_cboAiBackend.SelectedIndex == 1) // Ollama
            {
                url = _txtOllamaUrl.Text.Trim();
                model = _txtOllamaModel.Text.Trim();
            }
            else if (_cboAiBackend.SelectedIndex == 2) // Cloud
            {
                url = _txtCloudUrl.Text.Trim();
                model = _txtCloudModel.Text.Trim();
                apiKey = _txtCloudApiKey.Text.Trim();
            }
            else // llama.cpp
            {
                url = _txtServerUrl.Text.Trim();
                model = "default";
            }

            var (success, msg) = await _llmService.TestLlmConnectionDetailedAsync(url, model, apiKey);

            _btnTestLlm.Enabled = true;
            _lblLlmTestResult.ForeColor = success ? Color.DarkGreen : Color.Red;
            _lblLlmTestResult.Text = msg;
        }

        private async void OnRestartDaemonClick(object? sender, EventArgs e)
        {
            try
            {
                _btnRestartDaemon.Enabled = false;
                _lblDaemonStatus.ForeColor = Color.DarkOrange;
                _lblDaemonStatus.Text = "Restarting daemon...";

                // Ensure settings are saved first
                SaveCurrentValuesToConfig();

                // Start or restart daemon process
                bool success = await EnsureTrayDaemonRunningAsync(restart: true);

                if (success)
                {
                    _lblDaemonStatus.ForeColor = Color.DarkGreen;
                    _lblDaemonStatus.Text = "✓ System tray daemon is active and running.";
                }
                else
                {
                    _lblDaemonStatus.ForeColor = Color.Red;
                    _lblDaemonStatus.Text = "✗ Failed to start system tray daemon.";
                }
            }
            catch (Exception ex)
            {
                _lblDaemonStatus.ForeColor = Color.Red;
                _lblDaemonStatus.Text = $"Error: {ex.Message}";
            }
            finally
            {
                _btnRestartDaemon.Enabled = true;
            }
        }

        public static async Task<bool> EnsureTrayDaemonRunningAsync(bool restart = false)
        {
            try
            {
                string currentExe = Application.ExecutablePath;
                string procName = Path.GetFileNameWithoutExtension(currentExe);
                int currentPid = Process.GetCurrentProcess().Id;

                if (restart)
                {
                    // 1. Signal graceful exit event to old daemon so it deletes its tray icon
                    try
                    {
                        if (EventWaitHandle.TryOpenExisting(NativeTrayDaemon.ExitEventName, out var exitEvt))
                        {
                            exitEvt.Set();
                            exitEvt.Dispose();
                        }
                    }
                    catch { }

                    // 2. Find and wait for existing daemon processes to exit
                    var existingDaemons = Process.GetProcessesByName(procName)
                        .Where(p => p.Id != currentPid && p.MainWindowHandle == IntPtr.Zero)
                        .ToList();

                    foreach (var p in existingDaemons)
                    {
                        try
                        {
                            bool exited = await Task.Run(() => p.WaitForExit(1500));
                            if (!exited)
                            {
                                p.Kill();
                                await Task.Run(() => p.WaitForExit(1000));
                            }
                        }
                        catch { }
                    }

                    // 3. Small delay to ensure Windows OS finishes cleaning named mutexes
                    await Task.Delay(250);
                }

                // Check if already running via mutex (only if not a restart)
                if (!restart)
                {
                    bool alreadyRunning = Mutex.TryOpenExisting(@"Global\KerkenezMail_TrayDaemon_Mutex", out var existingMutex);
                    if (alreadyRunning)
                    {
                        existingMutex?.Dispose();
                        return true;
                    }
                }

                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = "--daemon",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                return proc != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EnsureTrayDaemonRunning] Error: {ex.Message}");
                return false;
            }
        }

        public static bool IsStartupWithWindowsEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                var val = key?.GetValue("KerkenezMailTray") as string;
                if (!string.IsNullOrWhiteSpace(val)) return true;
                var legacyVal = key?.GetValue("EmailSummarizerTray") as string;
                return !string.IsNullOrWhiteSpace(legacyVal);
            }
            catch
            {
                return false;
            }
        }

        private static void SetStartupWithWindows(bool enable)
        {
            try
            {
                // CurrentUser (HKCU) guarantees execution ONLY after the specific user completes interactive logon
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    if (enable)
                    {
                        key.SetValue("KerkenezMailTray", $"\"{Application.ExecutablePath}\" --daemon");
                        key.DeleteValue("EmailSummarizerTray", false);
                    }
                    else
                    {
                        key.DeleteValue("KerkenezMailTray", false);
                        key.DeleteValue("EmailSummarizerTray", false);
                    }
                }
            }
            catch
            {
                // Fallback
            }
        }

        private void SaveCurrentValuesToConfig()
        {
            var s = _configService.Settings;

            // AI Backend Selection
            if (_cboAiBackend.SelectedIndex == 1)
            {
                s.AiBackend = "Ollama";
            }
            else if (_cboAiBackend.SelectedIndex == 2)
            {
                s.AiBackend = "Cloud";
            }
            else if (_cboAiBackend.SelectedIndex == 3)
            {
                s.AiBackend = "None";
            }
            else
            {
                s.AiBackend = "LlamaCpp";
            }

            // Power Management
            s.DisableAiOnBattery = _chkDisableAiOnBattery.Checked;

            // llama.cpp settings
            s.LlamaModelPath = _txtModelPath.Text.Trim();
            s.LlamaServerPort = (int)_numPort.Value;
            s.LlamaGpuLayers = (int)_numGpuLayers.Value;
            s.LlamaContextSize = (int)_numContextSize.Value;
            s.LlamaServerUrl = _txtServerUrl.Text.Trim();
            s.AutoStartLlamaServer = _chkAutoStart.Checked;
            s.InstantVramUnload = _chkInstantVram.Checked;

            // Ollama settings
            s.OllamaServerUrl = _txtOllamaUrl.Text.Trim();
            s.OllamaModelName = _txtOllamaModel.Text.Trim();

            // Cloud settings
            s.CloudApiUrl = _txtCloudUrl.Text.Trim();
            s.CloudApiKey = _txtCloudApiKey.Text.Trim();
            s.CloudModelName = _txtCloudModel.Text.Trim();

            // Global LLM settings
            s.Temperature = (double)_numTemperature.Value;
            s.MaxTokens = (int)_numMaxTokens.Value;
            s.MaxSummaryEmailChars = _chkUnlimitedEmailChars.Checked ? 0 : (int)_numMaxSummaryChars.Value;

            // Email settings
            s.MaxEmailsPerAccount = (int)_numMaxEmails.Value;
            s.OnlyUnread = _chkOnlyUnread.Checked;
            s.MarkAsSeen = _chkMarkAsSeen.Checked;
            s.MultiSelectPreview = _cboMultiSelectPreview.SelectedIndex == 1 ? "FirstSelected" : "LastSelected";
            s.AttachmentDownloadPath = _txtAttachmentDownloadPath.Text.Trim();

            // UI & Layout settings
            s.CollapseSidebarByDefault = _chkCollapseSidebarByDefault.Checked;
            s.WindowWidthScale = (double)_numWindowWidthScale.Value / 100.0;
            s.WindowHeightScale = (double)_numWindowHeightScale.Value / 100.0;

            var screen = Screen.FromControl(this) ?? Screen.PrimaryScreen;
            var wa = screen?.WorkingArea ?? (Screen.PrimaryScreen != null ? Screen.PrimaryScreen.WorkingArea : new Rectangle(0, 0, 1920, 1080));
            int targetW = (int)Math.Round(wa.Width * s.WindowWidthScale);
            int targetH = (int)Math.Round(wa.Height * s.WindowHeightScale);
            s.WindowWidth = targetW;
            s.WindowHeight = targetH;

            // System Tray settings
            s.AlwaysKeepOn = _chkAlwaysKeepOn.Checked;
            s.EnableTrayNotifications = _chkEnableTrayNotifs.Checked;
            s.TrayRefreshIntervalMinutes = (int)_numTrayInterval.Value;
            s.StartWithWindows = _chkStartWithWindows.Checked;

            s.SystemPrompt = _txtPrompt.Text;

            // Language setting
            if (_cboLanguage.SelectedItem is LanguageComboItem selLang)
            {
                s.Language = selLang.Language.Code;
                LanguageManager.Instance.SetLanguage(selLang.Language.Code);
            }

            SetStartupWithWindows(s.StartWithWindows);
            _configService.SaveConfig();
        }

        private async void OnSaveSettingsClick(object? sender, EventArgs e)
        {
            SaveCurrentValuesToConfig();

            if (_chkAlwaysKeepOn.Checked)
            {
                await EnsureTrayDaemonRunningAsync(restart: false);
            }

            MessageBox.Show(Lang.T(StringKeys.SettingsSavedToast), Lang.T(StringKeys.CommonSuccess), MessageBoxButtons.OK, MessageBoxIcon.Information);
            _logger.Report("[✓] Configuration saved to config.json.");
            SettingsSaved?.Invoke();
        }

        private void OnResetDefaultsClick(object? sender, EventArgs e)
        {
            if (MessageBox.Show(Lang.T(StringKeys.SettingsResetConfirm), Lang.T(StringKeys.CommonWarning), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var defaults = AppSettings.CreateDefault();
                defaults.AccountIds = _configService.Settings.AccountIds;
                _configService.SaveConfig(defaults);
                LoadSettings();
                SettingsSaved?.Invoke();
            }
        }
    }
}
