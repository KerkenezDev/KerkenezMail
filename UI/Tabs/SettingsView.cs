using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        // AI Backend Selection & Containers
        private ComboBox _cboAiBackend = null!;
        private FlowLayoutPanel _pnlLlamaContainer = null!;
        private FlowLayoutPanel _pnlOllamaContainer = null!;
        private FlowLayoutPanel _pnlCloudContainer = null!;

        // 1. llama.cpp controls
        private TextBox _txtModelPath = null!;
        private Button _btnBrowseModel = null!;
        private NumericUpDown _numPort = null!;
        private NumericUpDown _numGpuLayers = null!;
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
            
            var lblSec1 = CreateSectionHeader("🤖  AI Engine & LLM Backend");

            var lblBackend = new Label
            {
                Text = "Select Inference Backend:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 0, 0, 4)
            };

            _cboAiBackend = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ContentW - 28,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 12)
            };
            _cboAiBackend.Items.Add("🦙  Local llama.cpp (Embedded GGUF)");
            _cboAiBackend.Items.Add("🦙  Local Ollama (localhost:11434)");
            _cboAiBackend.Items.Add("☁️  Cloud / Custom API (OpenAI, OpenRouter, Groq, DeepSeek)");
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

            _pnlLlamaContainer.Controls.Add(lblModel);
            _pnlLlamaContainer.Controls.Add(rowModel);
            _pnlLlamaContainer.Controls.Add(rowParams);
            _pnlLlamaContainer.Controls.Add(lblUrl);
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

            var lblOllamaInfo = new Label
            {
                Text = "💡 Connects directly to local Ollama. Ensure Ollama is running (`ollama serve` or desktop app).",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.75F, FontStyle.Italic),
                ForeColor = Color.FromArgb(70, 70, 70),
                Margin = new Padding(0, 0, 0, 8)
            };

            var lblOllamaUrl = new Label { Text = "Ollama Endpoint URL:", AutoSize = true, Margin = new Padding(0, 0, 0, 3), Font = new Font("Segoe UI", 8.75F) };
            _txtOllamaUrl = new TextBox { Width = ContentW - 28, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 0, 0, 8), Text = "http://127.0.0.1:11434/v1/chat/completions" };

            var lblOllamaModel = new Label { Text = "Ollama Model Name (e.g. llama3.2, qwen2.5:3b, mistral):", AutoSize = true, Margin = new Padding(0, 0, 0, 3), Font = new Font("Segoe UI", 8.75F) };
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
            var lblSuggestions = new Label { Text = "Suggestions:", AutoSize = true, Font = new Font("Segoe UI", 8.25F, FontStyle.Bold), Margin = new Padding(0, 4, 6, 0) };
            rowOllamaChips.Controls.Add(lblSuggestions);

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

            _pnlOllamaContainer.Controls.Add(lblOllamaInfo);
            _pnlOllamaContainer.Controls.Add(lblOllamaUrl);
            _pnlOllamaContainer.Controls.Add(_txtOllamaUrl);
            _pnlOllamaContainer.Controls.Add(lblOllamaModel);
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

            var lblPreset = new Label { Text = "Provider Preset:", AutoSize = true, Margin = new Padding(0, 0, 0, 3), Font = new Font("Segoe UI", 8.75F) };
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

            var lblCloudUrl = new Label { Text = "API Endpoint URL (/v1/chat/completions):", AutoSize = true, Margin = new Padding(0, 0, 0, 3), Font = new Font("Segoe UI", 8.75F) };
            _txtCloudUrl = new TextBox { Width = ContentW - 28, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 0, 0, 8), Text = "https://api.openai.com/v1/chat/completions" };

            var lblApiKey = new Label { Text = "API Key (Bearer Token):", AutoSize = true, Margin = new Padding(0, 0, 0, 3), Font = new Font("Segoe UI", 8.75F) };
            
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
                _btnToggleKeyVisibility.Text = _isApiKeyVisible ? "🔒 Hide" : "👁️ Show";
            };

            rowApiKey.Controls.Add(_txtCloudApiKey, 0, 0);
            rowApiKey.Controls.Add(_btnToggleKeyVisibility, 1, 0);

            var lblCloudModel = new Label { Text = "Model ID / Name (e.g. gpt-4o-mini, deepseek-chat):", AutoSize = true, Margin = new Padding(0, 0, 0, 3), Font = new Font("Segoe UI", 8.75F) };
            _txtCloudModel = new TextBox { Width = ContentW - 28, Font = new Font("Segoe UI", 9F), Margin = new Padding(0, 0, 0, 8), Text = "gpt-4o-mini" };

            _pnlCloudContainer.Controls.Add(lblPreset);
            _pnlCloudContainer.Controls.Add(_cboCloudPreset);
            _pnlCloudContainer.Controls.Add(lblCloudUrl);
            _pnlCloudContainer.Controls.Add(_txtCloudUrl);
            _pnlCloudContainer.Controls.Add(lblApiKey);
            _pnlCloudContainer.Controls.Add(rowApiKey);
            _pnlCloudContainer.Controls.Add(lblCloudModel);
            _pnlCloudContainer.Controls.Add(_txtCloudModel);

            // ------------------ 1D. Global Inference Settings ------------------
            var pnlGlobalParams = new FlowLayoutPanel
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
            var lblTemp = new Label { Text = "Temperature:", AutoSize = true, Font = new Font("Segoe UI", 8.75F), Margin = new Padding(0, 0, 0, 4) };
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
            pnlTemp.Controls.Add(lblTemp);
            pnlTemp.Controls.Add(_numTemperature);

            var pnlMaxTokens = new FlowLayoutPanel
            {
                Width = 220,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0)
            };
            var lblMaxTokens = new Label { Text = "Max Output Tokens:", AutoSize = true, Font = new Font("Segoe UI", 8.75F), Margin = new Padding(0, 0, 0, 4) };
            _numMaxTokens = new NumericUpDown
            {
                Width = 150,
                Height = 28,
                Minimum = 16,
                Maximum = 4096,
                Value = 350,
                Font = new Font("Segoe UI", 9.5F)
            };
            pnlMaxTokens.Controls.Add(lblMaxTokens);
            pnlMaxTokens.Controls.Add(_numMaxTokens);

            pnlGlobalParams.Controls.Add(pnlTemp);
            pnlGlobalParams.Controls.Add(pnlMaxTokens);

            // ------------------ 1E. Email Ingestion Length Limit ------------------
            var pnlEmailLengthContainer = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 4, 0, 10)
            };

            var lblSummaryCharsHeader = new Label
            {
                Text = "Max Email Length Sent to AI (Character Context Limit):",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.75F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 0, 0, 3)
            };

            var lblSummaryCharsDesc = new Label
            {
                Text = "Controls how many characters of the email body are sent to the AI for summarization. Full email text is always fetched and viewable in the app regardless of this limit.",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.25F),
                ForeColor = Color.FromArgb(110, 110, 110),
                Margin = new Padding(0, 0, 0, 6)
            };

            var rowCharControls = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 6)
            };

            _numMaxSummaryChars = new NumericUpDown
            {
                Width = 150,
                Height = 28,
                Minimum = 500,
                Maximum = 1000000,
                Increment = 500,
                Value = 4000,
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, 0, 14, 0)
            };

            _chkUnlimitedEmailChars = new CheckBox
            {
                Text = "♾️ Unlimited (Send entire email body to AI)",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                Margin = new Padding(0, 4, 0, 0),
                Cursor = Cursors.Hand
            };
            _chkUnlimitedEmailChars.CheckedChanged += (s, e) =>
            {
                _numMaxSummaryChars.Enabled = !_chkUnlimitedEmailChars.Checked;
            };

            rowCharControls.Controls.Add(_numMaxSummaryChars);
            rowCharControls.Controls.Add(_chkUnlimitedEmailChars);

            var rowCharPresets = new FlowLayoutPanel
            {
                Width = ContentW - 28,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 4)
            };

            var lblCharPresets = new Label
            {
                Text = "Presets:",
                AutoSize = true,
                Margin = new Padding(0, 3, 6, 0),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            var btnPreset4k = CreateCharLimitPresetChip("4,000 chars (Default)", 4000);
            var btnPreset8k = CreateCharLimitPresetChip("8,000 chars (Medium)", 8000);
            var btnPreset16k = CreateCharLimitPresetChip("16,000 chars (Long)", 16000);
            var btnPreset32k = CreateCharLimitPresetChip("32,000 chars (Very Long)", 32000);
            var btnPresetInf = CreateCharLimitPresetChip("♾️ Unlimited", 0);

            rowCharPresets.Controls.Add(lblCharPresets);
            rowCharPresets.Controls.Add(btnPreset4k);
            rowCharPresets.Controls.Add(btnPreset8k);
            rowCharPresets.Controls.Add(btnPreset16k);
            rowCharPresets.Controls.Add(btnPreset32k);
            rowCharPresets.Controls.Add(btnPresetInf);

            pnlEmailLengthContainer.Controls.Add(lblSummaryCharsHeader);
            pnlEmailLengthContainer.Controls.Add(lblSummaryCharsDesc);
            pnlEmailLengthContainer.Controls.Add(rowCharControls);
            pnlEmailLengthContainer.Controls.Add(rowCharPresets);

            // ------------------ Test Connection Row ------------------
            var rowTestLlm = new FlowLayoutPanel
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

            rowTestLlm.Controls.Add(_btnTestLlm);
            rowTestLlm.Controls.Add(_lblLlmTestResult);

            pnlLlmCard.Controls.Add(lblSec1);
            pnlLlmCard.Controls.Add(lblBackend);
            pnlLlmCard.Controls.Add(_cboAiBackend);
            pnlLlmCard.Controls.Add(_pnlLlamaContainer);
            pnlLlmCard.Controls.Add(_pnlOllamaContainer);
            pnlLlmCard.Controls.Add(_pnlCloudContainer);
            pnlLlmCard.Controls.Add(pnlGlobalParams);
            pnlLlmCard.Controls.Add(pnlEmailLengthContainer);
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
            var lblPreview = new Label { Text = "Multi-select preview email (Ctrl+click):", AutoSize = true, Margin = new Padding(0, 4, 8, 0), Font = new Font("Segoe UI", 9F) };
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
            rowPreview.Controls.Add(lblPreview);
            rowPreview.Controls.Add(_cboMultiSelectPreview);

            pnlEmailCard.Controls.Add(lblSec2);
            pnlEmailCard.Controls.Add(rowMax);
            pnlEmailCard.Controls.Add(_chkOnlyUnread);
            pnlEmailCard.Controls.Add(_chkMarkAsSeen);
            pnlEmailCard.Controls.Add(rowPreview);

            // ==================== 3. Interface & Layout Section ====================
            var pnlUiCard = CreateCardPanel(ContentW);
            var lblSecUi = CreateSectionHeader("🖥️  Interface & Layout");

            _chkCollapseSidebarByDefault = new CheckBox
            {
                Text = "Start with left sidebar collapsed by default (compact icon rail on launch)",
                AutoSize = true,
                Checked = false,
                Margin = new Padding(0, 0, 0, 10),
                Font = new Font("Segoe UI", 9F)
            };

            var lblScalingHeader = new Label
            {
                Text = "Default Launch Window Scaling (Relative to Display):",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0, 4, 0, 2)
            };

            var lblScalingDesc = new Label
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

            var lblWidth = new Label { Text = "Width Scale (%):", AutoSize = true, Margin = new Padding(0, 4, 6, 0), Font = new Font("Segoe UI", 9F) };
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

            var lblHeight = new Label { Text = "Height Scale (%):", AutoSize = true, Margin = new Padding(0, 4, 6, 0), Font = new Font("Segoe UI", 9F) };
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

            rowScaleControls.Controls.Add(lblWidth);
            rowScaleControls.Controls.Add(_numWindowWidthScale);
            rowScaleControls.Controls.Add(lblHeight);
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

            var lblPresets = new Label { Text = "Presets:", AutoSize = true, Margin = new Padding(0, 4, 6, 0), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(80, 80, 80) };
            var btnPresetDefault = CreatePresetChip("60% × 56% (Default)", 60.0M, 56.0M);
            var btnPresetCompact = CreatePresetChip("50% × 50% (Compact)", 50.0M, 50.0M);
            var btnPresetLarge = CreatePresetChip("75% × 70% (Large)", 75.0M, 70.0M);
            var btnPresetMax = CreatePresetChip("95% × 90% (Near Max)", 95.0M, 90.0M);

            rowPresets.Controls.Add(lblPresets);
            rowPresets.Controls.Add(btnPresetDefault);
            rowPresets.Controls.Add(btnPresetCompact);
            rowPresets.Controls.Add(btnPresetLarge);
            rowPresets.Controls.Add(btnPresetMax);

            _lblScalePreview = new Label
            {
                Text = "",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(70, 70, 70),
                Margin = new Padding(0, 2, 0, 2)
            };

            var btnCreateShortcuts = new Button
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
            btnCreateShortcuts.Click += (s, e) =>
            {
                bool ok = ShortcutService.CreateShortcuts();
                if (ok)
                {
                    MessageBox.Show("Shortcuts for Email Summarizer were successfully added to your Desktop and Start Menu!", "Shortcuts Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Could not create shortcuts.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            pnlUiCard.Controls.Add(lblSecUi);
            pnlUiCard.Controls.Add(_chkCollapseSidebarByDefault);
            pnlUiCard.Controls.Add(lblScalingHeader);
            pnlUiCard.Controls.Add(lblScalingDesc);
            pnlUiCard.Controls.Add(rowScaleControls);
            pnlUiCard.Controls.Add(rowPresets);
            pnlUiCard.Controls.Add(_lblScalePreview);
            pnlUiCard.Controls.Add(btnCreateShortcuts);

            // ==================== 4. System Tray Daemon & Notifications ====================
            var pnlTrayCard = CreateCardPanel(ContentW);
            var lblSecTray = CreateSectionHeader("🔔  System Tray Daemon & Notifications");

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
            var lblInterval = new Label { Text = "Check interval (minutes):", AutoSize = true, Margin = new Padding(0, 4, 8, 0), Font = new Font("Segoe UI", 9F) };
            _numTrayInterval = new NumericUpDown { Width = 75, Minimum = 1, Maximum = 120, Value = 5, Font = new Font("Segoe UI", 9F) };
            rowTrayInterval.Controls.Add(lblInterval);
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

            pnlTrayCard.Controls.Add(lblSecTray);
            pnlTrayCard.Controls.Add(_chkAlwaysKeepOn);
            pnlTrayCard.Controls.Add(_chkEnableTrayNotifs);
            pnlTrayCard.Controls.Add(rowTrayInterval);
            pnlTrayCard.Controls.Add(_chkStartWithWindows);
            pnlTrayCard.Controls.Add(rowDaemonAction);

            // ==================== 4. Prompt Template Section ====================
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
            _mainFlow.Controls.Add(pnlEmailCard);
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

        private void UpdateAiBackendPanelsVisibility()
        {
            int idx = _cboAiBackend.SelectedIndex;
            _pnlLlamaContainer.Visible = (idx == 0);
            _pnlOllamaContainer.Visible = (idx == 1);
            _pnlCloudContainer.Visible = (idx == 2);
            _lblLlmTestResult.Text = "";
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

            // AI Backend Selection
            if (string.Equals(s.AiBackend, "Ollama", StringComparison.OrdinalIgnoreCase))
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

            // llama.cpp settings
            _txtModelPath.Text = s.LlamaModelPath;
            _numPort.Value = Math.Max(_numPort.Minimum, Math.Min(_numPort.Maximum, s.LlamaServerPort));
            _numGpuLayers.Value = Math.Max(_numGpuLayers.Minimum, Math.Min(_numGpuLayers.Maximum, s.LlamaGpuLayers));
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

            _txtPrompt.Text = s.SystemPrompt;
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
                var screen = Screen.FromControl(this) ?? Screen.PrimaryScreen;
                var wa = screen?.WorkingArea ?? (Screen.PrimaryScreen != null ? Screen.PrimaryScreen.WorkingArea : new Rectangle(0, 0, 1920, 1080));
                double wScale = (double)_numWindowWidthScale.Value / 100.0;
                double hScale = (double)_numWindowHeightScale.Value / 100.0;
                int targetW = (int)Math.Round(wa.Width * wScale);
                int targetH = (int)Math.Round(wa.Height * hScale);
                _lblScalePreview.Text = $"Estimated resolution on active display: {targetW} × {targetH} px (Display working area: {wa.Width} × {wa.Height} px)";
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
                    bool alreadyRunning = Mutex.TryOpenExisting(@"Global\EmailSummarizer_TrayDaemon_Mutex", out var existingMutex);
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
                var val = key?.GetValue("EmailSummarizerTray") as string;
                return !string.IsNullOrWhiteSpace(val);
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
                        key.SetValue("EmailSummarizerTray", $"\"{Application.ExecutablePath}\" --daemon");
                    }
                    else
                    {
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
            else
            {
                s.AiBackend = "LlamaCpp";
            }

            // llama.cpp settings
            s.LlamaModelPath = _txtModelPath.Text.Trim();
            s.LlamaServerPort = (int)_numPort.Value;
            s.LlamaGpuLayers = (int)_numGpuLayers.Value;
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

            // UI & Layout settings
            s.CollapseSidebarByDefault = _chkCollapseSidebarByDefault.Checked;
            s.WindowWidthScale = (double)_numWindowWidthScale.Value / 100.0;
            s.WindowHeightScale = (double)_numWindowHeightScale.Value / 100.0;

            // System Tray settings
            s.AlwaysKeepOn = _chkAlwaysKeepOn.Checked;
            s.EnableTrayNotifications = _chkEnableTrayNotifs.Checked;
            s.TrayRefreshIntervalMinutes = (int)_numTrayInterval.Value;
            s.StartWithWindows = _chkStartWithWindows.Checked;

            s.SystemPrompt = _txtPrompt.Text;

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

            MessageBox.Show("Settings saved successfully!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _logger.Report("[✓] Configuration saved to config.json.");
        }

        private void OnResetDefaultsClick(object? sender, EventArgs e)
        {
            if (MessageBox.Show("Reset all settings to default values?", "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var defaults = AppSettings.CreateDefault();
                defaults.AccountIds = _configService.Settings.AccountIds;
                _configService.SaveConfig(defaults);
                LoadSettings();
            }
        }
    }
}
