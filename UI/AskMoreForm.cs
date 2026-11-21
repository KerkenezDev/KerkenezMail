using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KerkenezMail.Languages;
using KerkenezMail.Models;
using KerkenezMail.Services;
using KerkenezMail.UI.Controls;

namespace KerkenezMail.UI
{
    public class AskMoreForm : Form
    {
        private const double SingleModeScaleFactor = 0.70;
        private const double ChatModeScaleFactor = 0.80;
        private const double MorphDurationMs = 150.0;

        private readonly EmailItem _email;
        private readonly AskMoreSession _session;
        private readonly ConfigService _configService;
        private readonly LlamaServerManager _llamaManager;
        private readonly LlmSummarizerService _llmService;
        private readonly SemaphoreSlim _inferenceSemaphore;
        private readonly Func<bool> _isBatchSyncing;
        private readonly IProgress<string>? _logger;
        private readonly Action<string, string>? _statusUpdated;
        private readonly EventHandler<LanguageChangedEventArgs> _languageChangedHandler;

        // Shared Tooltip & Timers
        private ToolTip _sharedToolTip = null!;
        private System.Windows.Forms.Timer _linkHoverTimer = null!;
        private System.Windows.Forms.Timer _streamRenderTimer = null!;
        private System.Windows.Forms.Timer? _morphTimer;
        private MarkdownRichTextBox? _hoverLinkRtb;
        private string? _hoverLinkUrl;

        // Single Q&A View Controls (lazily bypassed when reopening directly in Chat mode)
        private Panel? _pnlSingleView;
        private Label? _lblPromptHeader;
        private TextBox? _txtQuestion;
        private AskActionButton? _btnSingleAsk;
        private ProgressBar? _singleProgressBar;
        private Label? _lblSingleAnswerHeader;
        private Label? _lblSingleStatus;
        private MarkdownRichTextBox? _rtbSingleAnswer;
        private Panel? _pnlSingleActions;
        private FlatIconButton? _btnSingleRetry;
        private FlatIconButton? _btnSingleCopy;
        private Panel? _pnlMoreQuestionsBar;
        private Button? _btnMoreQuestions;

        // Chat View Controls (lazily built on first transition to Chat mode)
        private Panel? _pnlChatView;
        private ChatScrollPanel? _chatScrollPanel;
        private Label? _lblChatStatus;
        private ProgressBar? _chatProgressBar;
        private ChatComposerBorderPanel? _pnlChatInputCard;
        private TextBox? _txtChatInput;
        private AskActionButton? _btnChatAsk;
        private readonly List<ChatTurnWidgets> _chatTurnWidgets = new List<ChatTurnWidgets>();

        // Generation & Animation State
        private CancellationTokenSource? _requestCts;
        private bool _isGenerating;
        private int _activeTurnIndex = -1;
        private string _latestStreamText = string.Empty;
        private bool _streamDirty;
        private MarkdownRichTextBox? _activeStreamRtb;
        private bool _activeStreamIsSingleView;
        private bool _isMorphAnimating;
        private bool _isReflowing;
        private double _morphStartRatioW;
        private double _morphStartRatioH;
        private double _morphTargetRatioW;
        private double _morphTargetRatioH;
        private double _morphBaseW;
        private double _morphBaseH;
        private double _morphCenterX;
        private double _morphCenterY;
        private Rectangle _morphWorkArea;
        private Stopwatch? _morphStopwatch;

        public AskMoreForm(
            EmailItem email,
            AskMoreSession session,
            ConfigService configService,
            LlamaServerManager llamaManager,
            LlmSummarizerService llmService,
            SemaphoreSlim inferenceSemaphore,
            Func<bool> isBatchSyncing,
            IProgress<string>? logger = null,
            Action<string, string>? statusUpdated = null,
            Form? owner = null)
        {
            _email = email ?? throw new ArgumentNullException(nameof(email));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _llamaManager = llamaManager ?? throw new ArgumentNullException(nameof(llamaManager));
            _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
            _inferenceSemaphore = inferenceSemaphore ?? throw new ArgumentNullException(nameof(inferenceSemaphore));
            _isBatchSyncing = isBatchSyncing ?? (() => false);
            _logger = logger;
            _statusUpdated = statusUpdated;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.DoubleBuffered = true;

            InitializeComponent(owner);
            _languageChangedHandler = (s, e) => ApplyLocalization();
            LanguageManager.Instance.LanguageChanged += _languageChangedHandler;
            ApplyLocalization();
            RestoreSessionView();
        }

        private (double WidthScale, double HeightScale) GetConfiguredWindowRatios()
        {
            double widthScale = _configService.Settings.WindowWidthScale > 0.1 && _configService.Settings.WindowWidthScale <= 1.0
                ? _configService.Settings.WindowWidthScale
                : 0.60;
            double heightScale = _configService.Settings.WindowHeightScale > 0.1 && _configService.Settings.WindowHeightScale <= 1.0
                ? _configService.Settings.WindowHeightScale
                : 0.56;
            return (widthScale, heightScale);
        }

        private Rectangle GetScreenWorkingArea(Form? preferredOwner = null)
        {
            var screen = (preferredOwner != null ? Screen.FromControl(preferredOwner) : null)
                         ?? (this.IsHandleCreated ? Screen.FromControl(this) : null)
                         ?? Screen.FromPoint(Cursor.Position)
                         ?? Screen.PrimaryScreen;

            return screen?.WorkingArea ?? (Screen.PrimaryScreen != null
                ? Screen.PrimaryScreen.WorkingArea
                : new Rectangle(0, 0, 1920, 1080));
        }

        private void InitializeComponent(Form? owner)
        {
            try
            {
                using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("KerkenezMail.app.ico");
                if (stream != null) this.Icon = new Icon(stream);
                else if (File.Exists("app.ico")) this.Icon = new Icon("app.ico");
            }
            catch { }

            var workingArea = GetScreenWorkingArea(owner);
            var (widthScale, heightScale) = GetConfiguredWindowRatios();
            double initialFactor = _session.IsChatMode ? ChatModeScaleFactor : SingleModeScaleFactor;

            int targetWidth = (int)Math.Round(workingArea.Width * widthScale * initialFactor);
            int targetHeight = (int)Math.Round(workingArea.Height * heightScale * initialFactor);

            int minWidth = Math.Min(360, workingArea.Width);
            int minHeight = Math.Min(260, workingArea.Height);

            targetWidth = Math.Clamp(targetWidth, minWidth, workingArea.Width);
            targetHeight = Math.Clamp(targetHeight, minHeight, workingArea.Height);

            this.MinimumSize = new Size(minWidth, minHeight);
            this.Size = new Size(targetWidth, targetHeight);
            this.StartPosition = FormStartPosition.Manual;

            int left = owner != null
                ? owner.Left + (owner.Width - targetWidth) / 2
                : workingArea.Left + (workingArea.Width - targetWidth) / 2;
            int top = owner != null
                ? owner.Top + (owner.Height - targetHeight) / 2
                : workingArea.Top + (workingArea.Height - targetHeight) / 2;

            left = Math.Clamp(left, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - targetWidth));
            top = Math.Clamp(top, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - targetHeight));
            this.Location = new Point(left, top);

            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.ShowInTaskbar = false;
            this.MaximizeBox = true;
            this.MinimizeBox = false;
            this.KeyPreview = true;
            this.KeyDown += OnFormKeyDown;

            _sharedToolTip = new ToolTip
            {
                ShowAlways = true,
                UseAnimation = true,
                UseFading = true,
                AutoPopDelay = 12000
            };

            _linkHoverTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _linkHoverTimer.Tick += OnLinkHoverTimerTick;

            _streamRenderTimer = new System.Windows.Forms.Timer { Interval = 75 };
            _streamRenderTimer.Tick += OnStreamRenderTimerTick;

            if (_session.IsChatMode)
            {
                EnsureChatViewCreated();
            }
            else
            {
                EnsureSingleViewCreated();
            }

            this.Shown += (s, e) =>
            {
                if (this.IsDisposed) return;
                if (_session.IsChatMode)
                {
                    ReflowChatBubbles();
                    ScrollChatToBottom();
                    if (_txtChatInput != null && _txtChatInput.CanFocus) _txtChatInput.Focus();
                }
                else
                {
                    if (_txtQuestion != null && _txtQuestion.CanFocus) _txtQuestion.Focus();
                }
            };
        }

        private void EnsureSingleViewCreated()
        {
            if (_pnlSingleView != null) return;

            float scale = this.DeviceDpi / 96f;

            _pnlSingleView = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            // 1. Top Prompt Panel
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding((int)(12 * scale), (int)(10 * scale), (int)(12 * scale), (int)(10 * scale))
            };
            pnlTop.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawLine(p, 0, pnlTop.Height - 1, pnlTop.Width, pnlTop.Height - 1);
            };

            _lblPromptHeader = new Label
            {
                Text = Lang.T(StringKeys.AskMorePromptLabel),
                Dock = DockStyle.Top,
                AutoSize = true,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 32, 44),
                Padding = new Padding(0, 0, 0, (int)(6 * scale))
            };

            var inputRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 1,
                Height = (int)(48 * scale),
                Margin = new Padding(0)
            };
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(116 * scale)));
            inputRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _txtQuestion = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                AcceptsReturn = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 9.25F),
                Margin = new Padding(0, 0, (int)(8 * scale), 0),
                PlaceholderText = Lang.T(StringKeys.AskMoreInputPlaceholder)
            };
            _txtQuestion.KeyDown += OnSingleQuestionKeyDown;

            _btnSingleAsk = new AskActionButton
            {
                Dock = DockStyle.Fill,
                CompactIconMode = false,
                Margin = new Padding(0),
                IdleText = Lang.T(StringKeys.AskMoreBtnAsk),
                GeneratingText = Lang.T(StringKeys.AskMoreGenerating),
                StopText = Lang.T(StringKeys.AskMoreBtnStop)
            };
            _btnSingleAsk.AskClicked += async (s, e) => await SubmitSingleQuestionAsync();
            _btnSingleAsk.StopClicked += (s, e) => CancelActiveGeneration();
            _btnSingleAsk.MouseEnter += (s, e) =>
            {
                if (_btnSingleAsk.IsGenerating)
                {
                    _sharedToolTip.SetToolTip(_btnSingleAsk, Lang.T(StringKeys.AskMoreTipStop));
                }
                else
                {
                    _sharedToolTip.SetToolTip(_btnSingleAsk, string.Empty);
                }
            };

            inputRow.Controls.Add(_txtQuestion, 0, 0);
            inputRow.Controls.Add(_btnSingleAsk, 1, 0);

            _singleProgressBar = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = Math.Max(3, (int)(3 * scale)),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            pnlTop.Controls.Add(inputRow);
            pnlTop.Controls.Add(_lblPromptHeader);
            pnlTop.Controls.Add(_singleProgressBar);

            // 2. Bottom [Have more questions?] Action Bar (hidden until answer completes)
            _pnlMoreQuestionsBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = (int)(44 * scale),
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding((int)(12 * scale), 0, (int)(12 * scale), (int)(10 * scale)),
                Visible = false
            };

            _btnMoreQuestions = new Button
            {
                Text = "💬  " + Lang.T(StringKeys.AskMoreBtnMoreQuestions),
                UseMnemonic = false,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(236, 243, 252),
                ForeColor = Color.FromArgb(0, 92, 190),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnMoreQuestions.FlatAppearance.BorderSize = 1;
            _btnMoreQuestions.FlatAppearance.BorderColor = Color.FromArgb(184, 210, 240);
            _btnMoreQuestions.FlatAppearance.MouseOverBackColor = Color.FromArgb(224, 236, 250);
            _btnMoreQuestions.FlatAppearance.MouseDownBackColor = Color.FromArgb(210, 228, 248);
            _btnMoreQuestions.Click += (s, e) => TransitionToChatModeAnimated();

            _pnlMoreQuestionsBar.Controls.Add(_btnMoreQuestions);

            // 3. Bottom Answer Card
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding((int)(12 * scale), (int)(10 * scale), (int)(12 * scale), (int)(10 * scale))
            };

            var pnlAnswerCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding((int)(12 * scale), (int)(8 * scale), (int)(12 * scale), (int)(8 * scale))
            };
            pnlAnswerCard.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(210, 222, 236), 1);
                e.Graphics.DrawRectangle(p, 0, 0, pnlAnswerCard.Width - 1, pnlAnswerCard.Height - 1);
            };

            var answerHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(24 * scale),
                Padding = new Padding(0, 0, 0, (int)(4 * scale))
            };

            _lblSingleAnswerHeader = new Label
            {
                Text = "✨ " + Lang.T(StringKeys.AskMoreAnswerHeader),
                Dock = DockStyle.Left,
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _lblSingleStatus = new Label
            {
                Text = string.Empty,
                Dock = DockStyle.Right,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Italic),
                ForeColor = Color.FromArgb(110, 118, 130),
                TextAlign = ContentAlignment.MiddleRight
            };

            answerHeader.Controls.Add(_lblSingleStatus);
            answerHeader.Controls.Add(_lblSingleAnswerHeader);

            _pnlSingleActions = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = (int)(28 * scale),
                Padding = new Padding(0, (int)(4 * scale), 0, 0),
                Visible = false
            };

            _btnSingleRetry = new FlatIconButton(FlatIconButton.IconKind.Retry)
            {
                Size = new Size((int)(26 * scale), (int)(24 * scale)),
                Location = new Point(0, (int)(4 * scale))
            };
            _btnSingleRetry.Click += async (s, e) => await RetrySingleQuestionAsync();
            _sharedToolTip.SetToolTip(_btnSingleRetry, Lang.T(StringKeys.AskMoreTipRetry));

            _btnSingleCopy = new FlatIconButton(FlatIconButton.IconKind.Copy)
            {
                Size = new Size((int)(26 * scale), (int)(24 * scale)),
                Location = new Point((int)(30 * scale), (int)(4 * scale))
            };
            _btnSingleCopy.Click += async (s, e) => await CopyTurnAnswerAsync(0, _btnSingleCopy);
            _sharedToolTip.SetToolTip(_btnSingleCopy, Lang.T(StringKeys.AskMoreTipCopy));

            _pnlSingleActions.Controls.Add(_btnSingleRetry);
            _pnlSingleActions.Controls.Add(_btnSingleCopy);

            _rtbSingleAnswer = new MarkdownRichTextBox
            {
                Dock = DockStyle.Fill,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                ForwardWheelToParent = false
            };
            AttachMarkdownRtbEvents(_rtbSingleAnswer);

            pnlAnswerCard.Controls.Add(_rtbSingleAnswer);
            pnlAnswerCard.Controls.Add(_pnlSingleActions);
            pnlAnswerCard.Controls.Add(answerHeader);
            pnlBottom.Controls.Add(pnlAnswerCard);

            _pnlSingleView.Controls.Add(pnlBottom);
            _pnlSingleView.Controls.Add(_pnlMoreQuestionsBar);
            _pnlSingleView.Controls.Add(pnlTop);

            this.Controls.Add(_pnlSingleView);
        }

        private void EnsureChatViewCreated()
        {
            if (_pnlChatView != null) return;

            float scale = this.DeviceDpi / 96f;

            _pnlChatView = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Visible = _session.IsChatMode
            };

            // 1. Scrollable Gemini-style Chat Feed
            _chatScrollPanel = new ChatScrollPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                AutoScroll = true
            };
            _chatScrollPanel.Resize += (s, e) =>
            {
                if (!_isMorphAnimating)
                {
                    ReflowChatBubbles();
                }
            };

            // 2. Bottom Chat Composer Bar
            var pnlComposerOuter = new Panel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding((int)(16 * scale), (int)(6 * scale), (int)(16 * scale), (int)(10 * scale))
            };
            pnlComposerOuter.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
                e.Graphics.DrawLine(pen, 0, 0, pnlComposerOuter.Width, 0);
            };

            _chatProgressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = Math.Max(3, (int)(3 * scale)),
                Style = ProgressBarStyle.Marquee,
                Visible = false,
                Margin = new Padding(0, 0, 0, (int)(4 * scale))
            };

            _lblChatStatus = new Label
            {
                Text = string.Empty,
                Dock = DockStyle.Top,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Italic),
                ForeColor = Color.FromArgb(110, 118, 130),
                Padding = new Padding(2, 0, 0, (int)(4 * scale)),
                Visible = false
            };

            int btnHolderW = (int)Math.Round(40 * scale);
            int btnSize = (int)Math.Round(34 * scale);

            _pnlChatInputCard = new ChatComposerBorderPanel
            {
                Dock = DockStyle.Top,
                Height = (int)Math.Round(52 * scale),
                Padding = new Padding((int)Math.Round(12 * scale), (int)Math.Round(7 * scale), (int)Math.Round(8 * scale), (int)Math.Round(7 * scale))
            };

            var composerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            composerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            composerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, btnHolderW));
            composerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _txtChatInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                AcceptsReturn = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.None,
                BackColor = Color.FromArgb(245, 247, 250),
                ForeColor = Color.FromArgb(25, 32, 44),
                Font = new Font("Segoe UI", 9.5F),
                Margin = new Padding(0, (int)Math.Round(2 * scale), (int)Math.Round(6 * scale), 0),
                PlaceholderText = Lang.T(StringKeys.AskMoreChatPlaceholder)
            };
            _txtChatInput.GotFocus += (s, e) => _pnlChatInputCard?.SetInputFocused(true);
            _txtChatInput.LostFocus += (s, e) => _pnlChatInputCard?.SetInputFocused(false);
            _txtChatInput.TextChanged += (s, e) => UpdateChatInputScrollBars();
            _txtChatInput.Resize += (s, e) => UpdateChatInputScrollBars();
            _txtChatInput.KeyDown += OnChatInputKeyDown;

            var btnHolder = new Panel
            {
                Dock = DockStyle.Fill,
                Size = new Size(btnHolderW, btnSize + 4),
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _btnChatAsk = new AskActionButton
            {
                Size = new Size(btnSize, btnSize),
                CompactIconMode = true,
                IdleText = Lang.T(StringKeys.AskMoreBtnAsk),
                GeneratingText = Lang.T(StringKeys.AskMoreGenerating),
                StopText = Lang.T(StringKeys.AskMoreBtnStop)
            };
            void CenterChatButton()
            {
                int x = Math.Max(0, (btnHolder.ClientSize.Width - btnSize) / 2 + (int)Math.Round(2 * scale));
                x = Math.Min(x, Math.Max(0, btnHolder.ClientSize.Width - btnSize));
                int y = Math.Max(0, (btnHolder.ClientSize.Height - btnSize) / 2);
                _btnChatAsk.SetBounds(x, y, btnSize, btnSize);
            }
            btnHolder.Resize += (s, e) => CenterChatButton();
            CenterChatButton();

            _btnChatAsk.AskClicked += async (s, e) => await SubmitChatQuestionAsync();
            _btnChatAsk.StopClicked += (s, e) => CancelActiveGeneration();
            _btnChatAsk.MouseEnter += (s, e) =>
            {
                if (_btnChatAsk.IsGenerating)
                {
                    _sharedToolTip.SetToolTip(_btnChatAsk, Lang.T(StringKeys.AskMoreTipStop));
                }
                else
                {
                    _sharedToolTip.SetToolTip(_btnChatAsk, string.Empty);
                }
            };

            btnHolder.Controls.Add(_btnChatAsk);
            composerLayout.Controls.Add(_txtChatInput, 0, 0);
            composerLayout.Controls.Add(btnHolder, 1, 0);
            _pnlChatInputCard.Controls.Add(composerLayout);

            pnlComposerOuter.Controls.Add(_pnlChatInputCard);
            pnlComposerOuter.Controls.Add(_lblChatStatus);
            pnlComposerOuter.Controls.Add(_chatProgressBar);

            _pnlChatView.Controls.Add(_chatScrollPanel);
            _pnlChatView.Controls.Add(pnlComposerOuter);

            this.Controls.Add(_pnlChatView);
        }

        private void RestoreSessionView()
        {
            if (_session.IsChatMode)
            {
                EnsureChatViewCreated();
                RebuildChatFeedFromSession();
                if (_txtChatInput != null && !string.IsNullOrEmpty(_session.DraftInput))
                {
                    _txtChatInput.Text = _session.DraftInput;
                }
                return;
            }

            if (_session.Turns.Count > 0)
            {
                var turn = _session.Turns[0];
                if (_txtQuestion != null)
                {
                    _txtQuestion.Text = !string.IsNullOrEmpty(_session.DraftInput)
                        ? _session.DraftInput
                        : turn.Question;
                }

                if (!string.IsNullOrWhiteSpace(turn.Answer))
                {
                    _rtbSingleAnswer?.SetMarkdown(turn.Answer, _email.ExtractedLinks, scrollToEnd: false);
                    if (_pnlSingleActions != null) _pnlSingleActions.Visible = true;
                    if (_pnlMoreQuestionsBar != null) _pnlMoreQuestionsBar.Visible = true;
                    if (turn.WasStopped) SetSingleStatusText(Lang.T(StringKeys.AskMoreStopped));
                }
                else
                {
                    SetSinglePlaceholderState();
                }
            }
            else
            {
                if (_txtQuestion != null && !string.IsNullOrEmpty(_session.DraftInput))
                {
                    _txtQuestion.Text = _session.DraftInput;
                }
                SetSinglePlaceholderState();
            }
        }

        private void ApplyLocalization()
        {
            if (this.IsDisposed) return;

            string subjectPreview = string.IsNullOrWhiteSpace(_email.Subject) ? "(No Subject)" : _email.Subject.Trim();
            if (subjectPreview.Length > 70)
            {
                subjectPreview = subjectPreview.Substring(0, 67).TrimEnd() + "...";
            }

            this.Text = $"{Lang.T(StringKeys.AskMoreTitle)} — {subjectPreview}";

            if (_lblPromptHeader != null) _lblPromptHeader.Text = Lang.T(StringKeys.AskMorePromptLabel);
            if (_txtQuestion != null) _txtQuestion.PlaceholderText = Lang.T(StringKeys.AskMoreInputPlaceholder);
            if (_btnSingleAsk != null)
            {
                _btnSingleAsk.IdleText = Lang.T(StringKeys.AskMoreBtnAsk);
                _btnSingleAsk.GeneratingText = Lang.T(StringKeys.AskMoreGenerating);
                _btnSingleAsk.StopText = Lang.T(StringKeys.AskMoreBtnStop);
            }
            if (_lblSingleAnswerHeader != null) _lblSingleAnswerHeader.Text = "✨ " + Lang.T(StringKeys.AskMoreAnswerHeader);
            if (_btnMoreQuestions != null) _btnMoreQuestions.Text = "💬  " + Lang.T(StringKeys.AskMoreBtnMoreQuestions);
            if (_btnSingleRetry != null) _sharedToolTip.SetToolTip(_btnSingleRetry, Lang.T(StringKeys.AskMoreTipRetry));
            if (_btnSingleCopy != null) _sharedToolTip.SetToolTip(_btnSingleCopy, Lang.T(StringKeys.AskMoreTipCopy));

            if (_txtChatInput != null) _txtChatInput.PlaceholderText = Lang.T(StringKeys.AskMoreChatPlaceholder);
            if (_btnChatAsk != null)
            {
                _btnChatAsk.IdleText = Lang.T(StringKeys.AskMoreBtnAsk);
                _btnChatAsk.GeneratingText = Lang.T(StringKeys.AskMoreGenerating);
                _btnChatAsk.StopText = Lang.T(StringKeys.AskMoreBtnStop);
            }

            foreach (var w in _chatTurnWidgets)
            {
                _sharedToolTip.SetToolTip(w.BtnRetry, Lang.T(StringKeys.AskMoreTipRetry));
                _sharedToolTip.SetToolTip(w.BtnCopy, Lang.T(StringKeys.AskMoreTipCopy));
            }

            if (!_session.IsChatMode && _session.Turns.Count == 0 && !_isGenerating && _rtbSingleAnswer != null)
            {
                SetSinglePlaceholderState();
            }
        }

        private void SetSinglePlaceholderState()
        {
            if (_rtbSingleAnswer == null) return;
            ResetLinkToolTip();
            if (_pnlSingleActions != null) _pnlSingleActions.Visible = false;
            if (_pnlMoreQuestionsBar != null) _pnlMoreQuestionsBar.Visible = false;
            _rtbSingleAnswer.SetStyledStatusText(
                Lang.T(StringKeys.AskMoreAnswerPlaceholder),
                Color.FromArgb(130, 136, 148),
                new Font("Segoe UI", 9.5F, FontStyle.Italic));
        }

        private void SetSingleStatusText(string text)
        {
            if (this.IsDisposed || _lblSingleStatus == null) return;
            _lblSingleStatus.Text = text ?? string.Empty;
        }

        private void SetChatStatusText(string text)
        {
            if (this.IsDisposed || _lblChatStatus == null) return;
            bool hasText = !string.IsNullOrWhiteSpace(text);
            _lblChatStatus.Text = text ?? string.Empty;
            _lblChatStatus.Visible = hasText;
        }

        private void OnFormKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                if (_isGenerating)
                {
                    CancelActiveGeneration();
                }
                else
                {
                    this.Close();
                }
            }
        }

        private void OnSingleQuestionKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift && !e.Control && !e.Alt)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                _ = SubmitSingleQuestionAsync();
            }
        }

        private void OnChatInputKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift && !e.Control && !e.Alt)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                _ = SubmitChatQuestionAsync();
            }
        }

        private void CancelActiveGeneration()
        {
            try
            {
                _requestCts?.Cancel();
            }
            catch { }
        }

        private async Task SubmitSingleQuestionAsync()
        {
            if (_txtQuestion == null || _btnSingleAsk == null || _rtbSingleAnswer == null || _isGenerating)
            {
                return;
            }

            string question = _txtQuestion.Text.Trim();
            if (string.IsNullOrWhiteSpace(question)) return;

            _session.Turns.Clear();
            _session.Turns.Add(new AskMoreTurn { Question = question });
            _session.DraftInput = string.Empty;

            if (_pnlSingleActions != null) _pnlSingleActions.Visible = false;
            if (_pnlMoreQuestionsBar != null) _pnlMoreQuestionsBar.Visible = false;

            await RunTurnGenerationAsync(0, isSingleView: true);
        }

        private async Task RetrySingleQuestionAsync()
        {
            if (_isGenerating || _txtQuestion == null) return;

            string edited = _txtQuestion.Text.Trim();
            if (_session.Turns.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(edited)) return;
                _session.Turns.Add(new AskMoreTurn { Question = edited });
            }
            else if (!string.IsNullOrWhiteSpace(edited))
            {
                _session.Turns[0].Question = edited;
            }

            _session.Turns[0].Answer = string.Empty;
            _session.Turns[0].WasStopped = false;
            _session.Turns[0].IsError = false;

            if (_pnlSingleActions != null) _pnlSingleActions.Visible = false;
            if (_pnlMoreQuestionsBar != null) _pnlMoreQuestionsBar.Visible = false;

            await RunTurnGenerationAsync(0, isSingleView: true);
        }

        private async Task SubmitChatQuestionAsync()
        {
            if (_txtChatInput == null || _isGenerating) return;

            string question = _txtChatInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(question)) return;

            _txtChatInput.Clear();
            _session.DraftInput = string.Empty;

            var turn = new AskMoreTurn { Question = question };
            int turnIndex = _session.Turns.Count;
            _session.Turns.Add(turn);

            EnsureChatViewCreated();
            AppendChatTurnWidget(turnIndex, turn, initiallyGenerating: true);
            ReflowChatBubbles();
            ScrollChatToBottom();

            await RunTurnGenerationAsync(turnIndex, isSingleView: false);
        }

        private async Task RetryChatTurnAsync(int turnIndex)
        {
            if (_isGenerating || turnIndex < 0 || turnIndex >= _session.Turns.Count) return;

            // Truncate subsequent turns so conversation stays stateful and consistent from this turn
            while (_session.Turns.Count > turnIndex + 1)
            {
                _session.Turns.RemoveAt(_session.Turns.Count - 1);
            }

            while (_chatTurnWidgets.Count > turnIndex + 1)
            {
                var lastWidget = _chatTurnWidgets[_chatTurnWidgets.Count - 1];
                _chatTurnWidgets.RemoveAt(_chatTurnWidgets.Count - 1);
                DisposeChatTurnWidget(lastWidget);
            }

            var targetTurn = _session.Turns[turnIndex];
            targetTurn.Answer = string.Empty;
            targetTurn.WasStopped = false;
            targetTurn.IsError = false;

            if (turnIndex < _chatTurnWidgets.Count)
            {
                var w = _chatTurnWidgets[turnIndex];
                w.ActionRow.Visible = false;
                w.LblNote.Text = string.Empty;
                w.LblNote.Visible = false;
            }

            await RunTurnGenerationAsync(turnIndex, isSingleView: false);
        }

        private async Task RunTurnGenerationAsync(int turnIndex, bool isSingleView)
        {
            if (turnIndex < 0 || turnIndex >= _session.Turns.Count) return;

            AskActionButton? activeBtn = isSingleView ? _btnSingleAsk : _btnChatAsk;
            MarkdownRichTextBox? targetRtb = isSingleView
                ? _rtbSingleAnswer
                : (turnIndex < _chatTurnWidgets.Count ? _chatTurnWidgets[turnIndex].AiAnswerBox : null);

            if (activeBtn == null || targetRtb == null) return;

            var settings = _configService.Settings;
            if (settings.IsAiDisabled)
            {
                string disabledMsg = Lang.T(StringKeys.AskMoreAiDisabled);
                _session.Turns[turnIndex].Answer = disabledMsg;
                _session.Turns[turnIndex].IsError = true;
                targetRtb.SetMarkdown(disabledMsg, _email.ExtractedLinks, scrollToEnd: false);
                FinishTurnUi(turnIndex, isSingleView, hasContent: true, wasStopped: false);
                return;
            }

            bool isLlama = string.Equals(settings.AiBackend, "LlamaCpp", StringComparison.OrdinalIgnoreCase);
            if (isLlama)
            {
                string? modelErr = null;
                if (string.IsNullOrWhiteSpace(settings.LlamaModelPath))
                {
                    modelErr = "No GGUF model selected. Go to Settings > AI Backend to select a model.";
                }
                else if (!File.Exists(settings.LlamaModelPath))
                {
                    string fileName = Path.GetFileName(settings.LlamaModelPath);
                    modelErr = string.IsNullOrWhiteSpace(fileName)
                        ? "Model file not found. Please verify the GGUF model path in Settings."
                        : $"Model file not found ({fileName}). Please verify the GGUF model path in Settings.";
                }

                if (modelErr != null)
                {
                    _session.Turns[turnIndex].Answer = modelErr;
                    _session.Turns[turnIndex].IsError = true;
                    targetRtb.SetMarkdown(modelErr, _email.ExtractedLinks, scrollToEnd: false);
                    FinishTurnUi(turnIndex, isSingleView, hasContent: true, wasStopped: false);
                    return;
                }
            }

            _requestCts?.Cancel();
            _requestCts?.Dispose();
            _requestCts = new CancellationTokenSource();
            var ct = _requestCts.Token;

            _isGenerating = true;
            _activeTurnIndex = turnIndex;
            _latestStreamText = string.Empty;
            Volatile.Write(ref _streamDirty, false);
            _activeStreamRtb = targetRtb;
            _activeStreamIsSingleView = isSingleView;

            activeBtn.IsGenerating = true;
            SetProgressBarVisible(isSingleView, true);
            SetRetryButtonsEnabled(false);
            SetActiveStatus(isSingleView, Lang.T(StringKeys.AskMoreThinking));

            ResetLinkToolTip();
            targetRtb.SetStyledStatusText(
                "⏳ " + Lang.T(StringKeys.AskMoreThinking),
                Color.FromArgb(90, 105, 125),
                new Font("Segoe UI", 9.5F, FontStyle.Italic));
            if (!isSingleView)
            {
                ReflowChatBubbles();
                ScrollChatToBottom();
            }

            _streamRenderTimer.Start();

            bool semaphoreAcquired = false;
            try
            {
                if (_inferenceSemaphore.CurrentCount == 0)
                {
                    SetActiveStatus(isSingleView, Lang.T(StringKeys.AskMoreWaitingQueue));
                }

                await _inferenceSemaphore.WaitAsync(ct);
                semaphoreAcquired = true;

                if (isLlama && settings.AutoStartLlamaServer)
                {
                    SetActiveStatus(isSingleView, Lang.T(StringKeys.AskMoreStartingModel));
                    _statusUpdated?.Invoke($"Answering question on \"{_email.Subject}\"...", "In Use (GPU)");

                    bool serverStarted = await _llamaManager.StartAsync(
                        settings.LlamaModelPath,
                        settings.LlamaServerPort,
                        settings.LlamaGpuLayers,
                        contextSize: settings.LlamaContextSize,
                        logger: _logger,
                        ct: ct);

                    if (!serverStarted)
                    {
                        const string failMsg = "Could not start llama-server. Please check Live Logs for details.";
                        _session.Turns[turnIndex].Answer = failMsg;
                        _session.Turns[turnIndex].IsError = true;
                        targetRtb.SetMarkdown(failMsg, _email.ExtractedLinks, scrollToEnd: false);
                        _statusUpdated?.Invoke(Lang.T(StringKeys.StatusModelLoadFailed), Lang.T(StringKeys.StatusVramFree));
                        FinishTurnUi(turnIndex, isSingleView, hasContent: true, wasStopped: false);
                        return;
                    }
                }

                SetActiveStatus(isSingleView, Lang.T(StringKeys.AskMoreThinking));

                var turnsPrefix = _session.Turns.Take(turnIndex + 1).ToList();
                var result = await _llmService.AskAboutEmailChatStreamAsync(
                    _email,
                    turnsPrefix,
                    settings,
                    onVisibleTextUpdated: visible =>
                    {
                        _latestStreamText = visible;
                        Volatile.Write(ref _streamDirty, true);
                    },
                    ct);

                _streamRenderTimer.Stop();
                Volatile.Write(ref _streamDirty, false);

                if (this.IsDisposed) return;

                var turn = _session.Turns[turnIndex];
                turn.Answer = result.Content;
                turn.WasStopped = result.WasStopped;
                turn.IsError = !result.Success && !result.WasStopped;

                if (result.WasStopped && string.IsNullOrWhiteSpace(result.Content))
                {
                    if (isSingleView)
                    {
                        SetSinglePlaceholderState();
                    }
                    else
                    {
                        targetRtb.SetStyledStatusText(
                            Lang.T(StringKeys.AskMoreStopped),
                            Color.FromArgb(130, 136, 148),
                            new Font("Segoe UI", 9.25F, FontStyle.Italic));
                    }
                    FinishTurnUi(turnIndex, isSingleView, hasContent: false, wasStopped: true);
                }
                else
                {
                    targetRtb.SetMarkdown(result.Content, _email.ExtractedLinks, scrollToEnd: false);
                    FinishTurnUi(turnIndex, isSingleView, hasContent: true, wasStopped: result.WasStopped);
                    if (!result.WasStopped)
                    {
                        _logger?.Report($"[✓] Answered follow-up question for: \"{_email.Subject}\"");
                    }
                }

                if (isLlama && settings.AutoStartLlamaServer)
                {
                    if (settings.InstantVramUnload && !_isBatchSyncing())
                    {
                        _llamaManager.Stop(_logger);
                        _statusUpdated?.Invoke(Lang.T(StringKeys.StatusReady), Lang.T(StringKeys.StatusVramFree));
                    }
                    else
                    {
                        _statusUpdated?.Invoke(Lang.T(StringKeys.StatusReady), Lang.T(StringKeys.StatusModelLoaded));
                    }
                }
                else
                {
                    string backendMetric = string.Equals(settings.AiBackend, "Ollama", StringComparison.OrdinalIgnoreCase)
                        ? Lang.T(StringKeys.StatusOllamaActive)
                        : Lang.T(StringKeys.StatusCloudActive);
                    _statusUpdated?.Invoke(Lang.T(StringKeys.StatusReady), backendMetric);
                }
            }
            catch (OperationCanceledException)
            {
                _streamRenderTimer.Stop();
                Volatile.Write(ref _streamDirty, false);
                if (!this.IsDisposed && turnIndex < _session.Turns.Count)
                {
                    string partial = _latestStreamText.Trim();
                    _session.Turns[turnIndex].Answer = partial;
                    _session.Turns[turnIndex].WasStopped = true;

                    if (!string.IsNullOrWhiteSpace(partial))
                    {
                        targetRtb.SetMarkdown(partial, _email.ExtractedLinks, scrollToEnd: false);
                        FinishTurnUi(turnIndex, isSingleView, hasContent: true, wasStopped: true);
                    }
                    else
                    {
                        if (isSingleView) SetSinglePlaceholderState();
                        else
                        {
                            targetRtb.SetStyledStatusText(
                                Lang.T(StringKeys.AskMoreStopped),
                                Color.FromArgb(130, 136, 148),
                                new Font("Segoe UI", 9.25F, FontStyle.Italic));
                        }
                        FinishTurnUi(turnIndex, isSingleView, hasContent: false, wasStopped: true);
                    }
                }
            }
            catch (Exception ex)
            {
                _streamRenderTimer.Stop();
                Volatile.Write(ref _streamDirty, false);
                if (!this.IsDisposed && turnIndex < _session.Turns.Count)
                {
                    string errText = $"Error: {ex.Message}";
                    _session.Turns[turnIndex].Answer = errText;
                    _session.Turns[turnIndex].IsError = true;
                    targetRtb.SetMarkdown(errText, _email.ExtractedLinks, scrollToEnd: false);
                    FinishTurnUi(turnIndex, isSingleView, hasContent: true, wasStopped: false);
                }
            }
            finally
            {
                _streamRenderTimer.Stop();
                if (semaphoreAcquired)
                {
                    _inferenceSemaphore.Release();
                }

                if (!this.IsDisposed)
                {
                    _isGenerating = false;
                    _activeTurnIndex = -1;
                    _activeStreamRtb = null;
                    activeBtn.IsGenerating = false;
                    SetProgressBarVisible(isSingleView, false);
                    SetRetryButtonsEnabled(true);
                }
            }
        }

        private void OnStreamRenderTimerTick(object? sender, EventArgs e)
        {
            if (this.IsDisposed || !_isGenerating || _activeStreamRtb == null) return;
            if (!Volatile.Read(ref _streamDirty)) return;

            string snapshot = _latestStreamText;
            Volatile.Write(ref _streamDirty, false);
            if (string.IsNullOrEmpty(snapshot)) return;

            bool stickToBottom = !_activeStreamIsSingleView && IsChatNearBottom();
            _activeStreamRtb.SetMarkdown(snapshot, _email.ExtractedLinks, scrollToEnd: _activeStreamIsSingleView);

            if (!_activeStreamIsSingleView)
            {
                RepositionChatTurnVerticals();
                if (stickToBottom)
                {
                    ScrollChatToBottom();
                }
            }
        }

        private void FinishTurnUi(int turnIndex, bool isSingleView, bool hasContent, bool wasStopped)
        {
            if (this.IsDisposed) return;

            if (isSingleView)
            {
                SetSingleStatusText(wasStopped ? Lang.T(StringKeys.AskMoreStopped) : string.Empty);
                if (_pnlSingleActions != null) _pnlSingleActions.Visible = true;
                if (_btnSingleCopy != null) _btnSingleCopy.Visible = hasContent;
                if (_pnlMoreQuestionsBar != null) _pnlMoreQuestionsBar.Visible = hasContent;
            }
            else
            {
                SetChatStatusText(string.Empty);
                if (turnIndex >= 0 && turnIndex < _chatTurnWidgets.Count)
                {
                    var w = _chatTurnWidgets[turnIndex];
                    w.ActionRow.Visible = true;
                    w.BtnCopy.Visible = hasContent;
                    w.LblNote.Text = wasStopped ? Lang.T(StringKeys.AskMoreStopped) : string.Empty;
                    w.LblNote.Visible = wasStopped;
                }
                ReflowChatBubbles();
                ScrollChatToBottom();
            }
        }

        private void SetProgressBarVisible(bool isSingleView, bool visible)
        {
            if (isSingleView)
            {
                if (_singleProgressBar != null) _singleProgressBar.Visible = visible;
            }
            else
            {
                if (_chatProgressBar != null) _chatProgressBar.Visible = visible;
            }
        }

        private void SetActiveStatus(bool isSingleView, string text)
        {
            if (isSingleView) SetSingleStatusText(text);
            else SetChatStatusText(text);
        }

        private void SetRetryButtonsEnabled(bool enabled)
        {
            if (_btnSingleRetry != null) _btnSingleRetry.Enabled = enabled;
            foreach (var w in _chatTurnWidgets)
            {
                w.BtnRetry.Enabled = enabled;
            }
        }

        private void UpdateChatInputScrollBars()
        {
            if (_txtChatInput == null || _txtChatInput.IsDisposed || _txtChatInput.ClientSize.Width <= 20) return;

            string text = _txtChatInput.Text;
            int textWidth = Math.Max(40, _txtChatInput.ClientSize.Width - 6);
            int actualHeight = string.IsNullOrEmpty(text)
                ? 0
                : TextRenderer.MeasureText(
                    text.EndsWith('\n') ? text + " " : text,
                    _txtChatInput.Font,
                    new Size(textWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;

            var desiredBars = actualHeight > _txtChatInput.ClientSize.Height ? ScrollBars.Vertical : ScrollBars.None;
            if (_txtChatInput.ScrollBars != desiredBars)
            {
                _txtChatInput.ScrollBars = desiredBars;
            }
        }

        private void TransitionToChatModeAnimated()
        {
            if (this.IsDisposed || _session.IsChatMode || _isGenerating) return;

            _session.IsChatMode = true;
            _session.DraftInput = string.Empty;

            EnsureChatViewCreated();

            this.SuspendLayout();
            if (_pnlSingleView != null) _pnlSingleView.Visible = false;
            if (_pnlChatView != null) _pnlChatView.Visible = true;
            this.ResumeLayout(true);
            this.PerformLayout();

            if (_chatScrollPanel != null)
            {
                _chatScrollPanel.AutoScrollPosition = Point.Empty;
            }
            RebuildChatFeedFromSession();

            _morphWorkArea = GetScreenWorkingArea();
            var (widthScale, heightScale) = GetConfiguredWindowRatios();

            _morphBaseW = Math.Max(1.0, _morphWorkArea.Width * widthScale);
            _morphBaseH = Math.Max(1.0, _morphWorkArea.Height * heightScale);

            _morphStartRatioW = this.Width / _morphBaseW;
            _morphStartRatioH = this.Height / _morphBaseH;
            _morphTargetRatioW = Math.Max(_morphStartRatioW, ChatModeScaleFactor);
            _morphTargetRatioH = Math.Max(_morphStartRatioH, ChatModeScaleFactor);

            _morphCenterX = this.Left + (this.Width / 2.0);
            _morphCenterY = this.Top + (this.Height / 2.0);

            bool needsMorph = this.WindowState == FormWindowState.Normal &&
                              (Math.Abs(_morphTargetRatioW - _morphStartRatioW) >= 0.005 ||
                               Math.Abs(_morphTargetRatioH - _morphStartRatioH) >= 0.005);

            if (!needsMorph)
            {
                ReflowChatBubbles();
                ScrollChatToBottom();
                if (_txtChatInput != null && _txtChatInput.CanFocus) _txtChatInput.Focus();
                return;
            }

            _isMorphAnimating = true;
            _morphStopwatch = Stopwatch.StartNew();
            _morphTimer?.Stop();
            _morphTimer?.Dispose();
            _morphTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _morphTimer.Tick += OnMorphTimerTick;
            _morphTimer.Start();
        }

        private void OnMorphTimerTick(object? sender, EventArgs e)
        {
            if (this.IsDisposed || _morphStopwatch == null)
            {
                StopMorphAnimation();
                return;
            }

            double t = Math.Clamp(_morphStopwatch.Elapsed.TotalMilliseconds / MorphDurationMs, 0.0, 1.0);
            double ease = 1.0 - Math.Pow(1.0 - t, 3.0); // Cubic ease-out

            double curRatioW = _morphStartRatioW + (_morphTargetRatioW - _morphStartRatioW) * ease;
            double curRatioH = _morphStartRatioH + (_morphTargetRatioH - _morphStartRatioH) * ease;

            int w = Math.Clamp((int)Math.Round(_morphBaseW * curRatioW), this.MinimumSize.Width, _morphWorkArea.Width);
            int h = Math.Clamp((int)Math.Round(_morphBaseH * curRatioH), this.MinimumSize.Height, _morphWorkArea.Height);

            int x = (int)Math.Round(_morphCenterX - (w / 2.0));
            int y = (int)Math.Round(_morphCenterY - (h / 2.0));
            x = Math.Clamp(x, _morphWorkArea.Left, Math.Max(_morphWorkArea.Left, _morphWorkArea.Right - w));
            y = Math.Clamp(y, _morphWorkArea.Top, Math.Max(_morphWorkArea.Top, _morphWorkArea.Bottom - h));

            this.SetBounds(x, y, w, h);

            if (t >= 1.0)
            {
                StopMorphAnimation();
                this.PerformLayout();
                ReflowChatBubbles();
                ScrollChatToBottom();
                if (_txtChatInput != null && _txtChatInput.CanFocus)
                {
                    _txtChatInput.Focus();
                }
            }
        }

        private void StopMorphAnimation()
        {
            _morphTimer?.Stop();
            _morphTimer?.Dispose();
            _morphTimer = null;
            _morphStopwatch = null;
            _isMorphAnimating = false;
        }

        private void RebuildChatFeedFromSession()
        {
            if (_chatScrollPanel == null) return;

            _chatScrollPanel.SuspendLayout();
            foreach (var w in _chatTurnWidgets)
            {
                DisposeChatTurnWidget(w);
            }
            _chatTurnWidgets.Clear();

            for (int i = 0; i < _session.Turns.Count; i++)
            {
                AppendChatTurnWidget(i, _session.Turns[i], initiallyGenerating: false);
            }

            _chatScrollPanel.ResumeLayout(false);
            ReflowChatBubbles();
        }

        private void AppendChatTurnWidget(int turnIndex, AskMoreTurn turn, bool initiallyGenerating)
        {
            if (_chatScrollPanel == null) return;

            float scale = this.DeviceDpi / 96f;
            int initialViewportW = _chatScrollPanel.ClientSize.Width >= 120
                ? _chatScrollPanel.ClientSize.Width
                : this.ClientSize.Width;
            var (_, initialContentW, _) = GetChatLayoutMetrics(initialViewportW);

            var userBubble = new UserMessageBubble(turn.Question);
            var aiRtb = new MarkdownRichTextBox
            {
                Width = initialContentW,
                Height = (int)(32 * scale),
                ScrollBars = RichTextBoxScrollBars.None,
                ForwardWheelToParent = true,
                AutoSizeHeight = true
            };
            AttachMarkdownRtbEvents(aiRtb);

            aiRtb.AutoHeightChanged += (s, e) =>
            {
                if (!_isReflowing && !_isMorphAnimating)
                {
                    RepositionChatTurnVerticals();
                }
            };

            var actionRow = new Panel
            {
                Height = (int)(26 * scale),
                BackColor = Color.White,
                Visible = !initiallyGenerating
            };

            var btnRetry = new FlatIconButton(FlatIconButton.IconKind.Retry)
            {
                Size = new Size((int)(26 * scale), (int)(24 * scale)),
                Location = new Point(0, 0),
                Enabled = !_isGenerating
            };
            int capturedIndex = turnIndex;
            btnRetry.Click += async (s, e) => await RetryChatTurnAsync(capturedIndex);
            _sharedToolTip.SetToolTip(btnRetry, Lang.T(StringKeys.AskMoreTipRetry));

            var btnCopy = new FlatIconButton(FlatIconButton.IconKind.Copy)
            {
                Size = new Size((int)(26 * scale), (int)(24 * scale)),
                Location = new Point((int)(30 * scale), 0),
                Visible = !string.IsNullOrWhiteSpace(turn.Answer)
            };
            btnCopy.Click += async (s, e) => await CopyTurnAnswerAsync(capturedIndex, btnCopy);
            _sharedToolTip.SetToolTip(btnCopy, Lang.T(StringKeys.AskMoreTipCopy));

            var lblNote = new Label
            {
                Text = turn.WasStopped ? Lang.T(StringKeys.AskMoreStopped) : string.Empty,
                AutoSize = true,
                Location = new Point((int)(62 * scale), (int)(4 * scale)),
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.FromArgb(125, 132, 145),
                Visible = turn.WasStopped
            };

            actionRow.Controls.Add(btnRetry);
            actionRow.Controls.Add(btnCopy);
            actionRow.Controls.Add(lblNote);

            _chatScrollPanel.Controls.Add(userBubble);
            _chatScrollPanel.Controls.Add(aiRtb);
            _chatScrollPanel.Controls.Add(actionRow);

            var widgets = new ChatTurnWidgets(turnIndex, userBubble, aiRtb, actionRow, btnRetry, btnCopy, lblNote);
            _chatTurnWidgets.Add(widgets);

            if (!initiallyGenerating)
            {
                if (!string.IsNullOrWhiteSpace(turn.Answer))
                {
                    aiRtb.SetMarkdown(turn.Answer, _email.ExtractedLinks, scrollToEnd: false);
                }
                else if (turn.WasStopped)
                {
                    aiRtb.SetStyledStatusText(
                        Lang.T(StringKeys.AskMoreStopped),
                        Color.FromArgb(130, 136, 148),
                        new Font("Segoe UI", 9.25F, FontStyle.Italic));
                }
            }
        }

        private void DisposeChatTurnWidget(ChatTurnWidgets w)
        {
            if (_chatScrollPanel != null)
            {
                _chatScrollPanel.Controls.Remove(w.UserBubble);
                _chatScrollPanel.Controls.Remove(w.AiAnswerBox);
                _chatScrollPanel.Controls.Remove(w.ActionRow);
            }
            w.UserBubble.Dispose();
            w.AiAnswerBox.Dispose();
            w.ActionRow.Dispose();
        }

        private void ReflowChatBubbles()
        {
            if (_isReflowing || _isMorphAnimating || _chatScrollPanel == null || !_session.IsChatMode) return;

            _isReflowing = true;
            _chatScrollPanel.SuspendLayout();
            try
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    int widthBefore = _chatScrollPanel.ClientSize.Width;
                    LayoutChatPass(widthBefore);
                    if (_chatScrollPanel.ClientSize.Width == widthBefore)
                    {
                        break;
                    }
                }
            }
            finally
            {
                _chatScrollPanel.ResumeLayout(false);
                _isReflowing = false;
            }
        }

        private void RepositionChatTurnVerticals()
        {
            if (_isReflowing || _isMorphAnimating || _chatScrollPanel == null || !_session.IsChatMode) return;
            _isReflowing = true;
            _chatScrollPanel.SuspendLayout();
            try
            {
                LayoutChatPass(_chatScrollPanel.ClientSize.Width, measureWidths: false);
            }
            finally
            {
                _chatScrollPanel.ResumeLayout(false);
                _isReflowing = false;
            }
        }

        private (int SidePad, int ContentW, int MaxBubbleW) GetChatLayoutMetrics(int viewportW)
        {
            float scale = this.DeviceDpi / 96f;
            int effectiveW = viewportW >= 120 ? viewportW : Math.Max(220, this.ClientSize.Width);
            int safeViewportW = Math.Max(220, effectiveW);
            int minSidePad = Math.Min((int)(14 * scale), safeViewportW / 8);
            int maxSidePad = Math.Max(minSidePad, Math.Min((int)(48 * scale), safeViewportW / 6));
            int sidePad = Math.Clamp((int)Math.Round(safeViewportW * 0.055), minSidePad, maxSidePad);
            int contentW = Math.Max(120, safeViewportW - (sidePad * 2));
            int minBubbleW = Math.Min((int)(140 * scale), contentW);
            int maxBubbleW = Math.Clamp((int)Math.Round(contentW * 0.72), minBubbleW, contentW);
            return (sidePad, contentW, maxBubbleW);
        }

        private void LayoutChatPass(int viewportW, bool measureWidths = true)
        {
            if (_chatScrollPanel == null) return;

            float scale = this.DeviceDpi / 96f;
            var (sidePad, contentW, maxBubbleW) = GetChatLayoutMetrics(viewportW);

            int scrollOffsetY = _chatScrollPanel.AutoScrollPosition.Y;
            int y = (int)(16 * scale);

            for (int i = 0; i < _chatTurnWidgets.Count; i++)
            {
                var w = _chatTurnWidgets[i];

                if (measureWidths)
                {
                    w.UserBubble.UpdateSizeForMaxWidth(maxBubbleW);
                }

                int bubbleX = sidePad + Math.Max(0, contentW - w.UserBubble.Width);
                w.UserBubble.Location = new Point(bubbleX, y + scrollOffsetY);
                y += w.UserBubble.Height + (int)(10 * scale);

                if (measureWidths && w.AiAnswerBox.Width != contentW)
                {
                    w.AiAnswerBox.Width = contentW;
                }

                if (w.AiAnswerBox.MeasuredContentHeight > 0)
                {
                    w.AiAnswerBox.ApplyMeasuredHeight((int)(24 * scale), (int)(8 * scale));
                }
                else if (!w.AiAnswerBox.IsHandleCreated)
                {
                    int fallbackH = TextRenderer.MeasureText(
                        w.AiAnswerBox.Text,
                        w.AiAnswerBox.Font,
                        new Size(Math.Max(100, contentW), int.MaxValue),
                        TextFormatFlags.WordBreak).Height + (int)(14 * scale);
                    w.AiAnswerBox.Height = Math.Max((int)(26 * scale), fallbackH);
                }

                w.AiAnswerBox.Location = new Point(sidePad, y + scrollOffsetY);
                y += w.AiAnswerBox.Height + (int)(2 * scale);

                w.ActionRow.Width = contentW;
                w.ActionRow.Location = new Point(sidePad, y + scrollOffsetY);
                y += (w.ActionRow.Visible ? w.ActionRow.Height : 0) + (int)(18 * scale);
            }

            _chatScrollPanel.AutoScrollMinSize = new Size(0, Math.Max(0, y));
        }

        private bool IsChatNearBottom()
        {
            if (_chatScrollPanel == null) return true;
            if (!_chatScrollPanel.VerticalScroll.Visible) return true;
            int maxScroll = Math.Max(0, _chatScrollPanel.VerticalScroll.Maximum - _chatScrollPanel.VerticalScroll.LargeChange + 1);
            return _chatScrollPanel.VerticalScroll.Value >= maxScroll - 36;
        }

        private void ScrollChatToBottom()
        {
            if (_chatScrollPanel == null || !_chatScrollPanel.IsHandleCreated) return;
            int totalH = _chatScrollPanel.AutoScrollMinSize.Height;
            int viewH = _chatScrollPanel.ClientSize.Height;
            if (totalH > viewH)
            {
                _chatScrollPanel.AutoScrollPosition = new Point(0, totalH - viewH);
            }
        }

        private void AttachMarkdownRtbEvents(MarkdownRichTextBox rtb)
        {
            rtb.LinkHoverRequested += (box, url) =>
            {
                _hoverLinkRtb = box;
                _hoverLinkUrl = url;
                _linkHoverTimer.Stop();
                _linkHoverTimer.Start();
            };
            rtb.LinkHoverCleared += box =>
            {
                if (_hoverLinkRtb == box)
                {
                    ResetLinkToolTip();
                }
            };
            rtb.LinkOpenFailed += err => _logger?.Report($"[!] Failed to open link: {err}");
        }

        private void OnLinkHoverTimerTick(object? sender, EventArgs e)
        {
            _linkHoverTimer.Stop();
            if (this.IsDisposed || _hoverLinkRtb == null || _hoverLinkRtb.IsDisposed || string.IsNullOrWhiteSpace(_hoverLinkUrl))
            {
                ResetLinkToolTip();
                return;
            }

            var clientPt = _hoverLinkRtb.PointToClient(Cursor.Position);
            if (!_hoverLinkRtb.ClientRectangle.Contains(clientPt))
            {
                ResetLinkToolTip();
                return;
            }

            var screen = Screen.FromControl(_hoverLinkRtb);
            string formattedTip = MarkdownRtfRenderer.FormatUrlForToolTip(_hoverLinkUrl, screen.WorkingArea.Width);

            int tipX = clientPt.X + 16;
            int tipY = clientPt.Y + 22;

            Point screenPt = Cursor.Position;
            int maxTipWidth = Math.Clamp((int)(screen.WorkingArea.Width * 0.42), 320, 620);
            if (screenPt.X + maxTipWidth > screen.WorkingArea.Right)
            {
                tipX = Math.Max(10, clientPt.X - maxTipWidth + 30);
            }

            _sharedToolTip.Show(formattedTip, _hoverLinkRtb, tipX, tipY, 10000);
        }

        private void ResetLinkToolTip()
        {
            _linkHoverTimer.Stop();
            if (_hoverLinkRtb != null && !_hoverLinkRtb.IsDisposed)
            {
                try
                {
                    _sharedToolTip.Hide(_hoverLinkRtb);
                }
                catch { }
            }
            _hoverLinkRtb = null;
            _hoverLinkUrl = null;
        }

        private async Task CopyTurnAnswerAsync(int turnIndex, FlatIconButton copyBtn)
        {
            if (turnIndex < 0 || turnIndex >= _session.Turns.Count) return;
            string answer = _session.Turns[turnIndex].Answer;
            if (string.IsNullOrWhiteSpace(answer)) return;

            try
            {
                Clipboard.SetText(answer);
                copyBtn.ShowCopiedFlash(true);
                await Task.Delay(1400);
                if (!this.IsDisposed && !copyBtn.IsDisposed)
                {
                    copyBtn.ShowCopiedFlash(false);
                }
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_session.IsChatMode)
            {
                _session.DraftInput = _txtChatInput?.Text ?? string.Empty;
            }
            else if (_txtQuestion != null)
            {
                _session.DraftInput = _txtQuestion.Text;
            }

            if (_isGenerating)
            {
                CancelActiveGeneration();
                if (_activeTurnIndex >= 0 && _activeTurnIndex < _session.Turns.Count)
                {
                    string partial = _latestStreamText.Trim();
                    if (!string.IsNullOrWhiteSpace(partial))
                    {
                        _session.Turns[_activeTurnIndex].Answer = partial;
                        _session.Turns[_activeTurnIndex].WasStopped = true;
                    }
                    else if (!_session.IsChatMode && string.IsNullOrWhiteSpace(_session.Turns[_activeTurnIndex].Answer))
                    {
                        _session.Turns.RemoveAt(_activeTurnIndex);
                    }
                    else if (string.IsNullOrWhiteSpace(_session.Turns[_activeTurnIndex].Answer))
                    {
                        _session.Turns[_activeTurnIndex].WasStopped = true;
                    }
                }
            }

            StopMorphAnimation();
            _streamRenderTimer.Stop();
            ResetLinkToolTip();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                LanguageManager.Instance.LanguageChanged -= _languageChangedHandler;
                CancelActiveGeneration();
                try
                {
                    _requestCts?.Dispose();
                    _requestCts = null;
                }
                catch { }

                StopMorphAnimation();
                _streamRenderTimer.Stop();
                _streamRenderTimer.Dispose();
                _linkHoverTimer.Stop();
                _linkHoverTimer.Dispose();
                _sharedToolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Lightweight Chat UI Helper Controls

        private sealed class ChatTurnWidgets
        {
            public int TurnIndex { get; }
            public UserMessageBubble UserBubble { get; }
            public MarkdownRichTextBox AiAnswerBox { get; }
            public Panel ActionRow { get; }
            public FlatIconButton BtnRetry { get; }
            public FlatIconButton BtnCopy { get; }
            public Label LblNote { get; }

            public ChatTurnWidgets(
                int turnIndex,
                UserMessageBubble userBubble,
                MarkdownRichTextBox aiAnswerBox,
                Panel actionRow,
                FlatIconButton btnRetry,
                FlatIconButton btnCopy,
                Label lblNote)
            {
                TurnIndex = turnIndex;
                UserBubble = userBubble;
                AiAnswerBox = aiAnswerBox;
                ActionRow = actionRow;
                BtnRetry = btnRetry;
                BtnCopy = btnCopy;
                LblNote = lblNote;
            }
        }

        private sealed class ChatScrollPanel : Panel
        {
            public ChatScrollPanel()
            {
                this.DoubleBuffered = true;
                this.AutoScroll = true;
            }

            protected override Point ScrollToControl(Control activeControl)
            {
                // Prevent WinForms from jumping scroll position on child focus/selection changes
                return this.DisplayRectangle.Location;
            }
        }

        private sealed class ChatComposerBorderPanel : Panel
        {
            private bool _isFocused;

            public ChatComposerBorderPanel()
            {
                this.DoubleBuffered = true;
                this.ResizeRedraw = true;
                this.BackColor = Color.White;
            }

            public void SetInputFocused(bool focused)
            {
                if (_isFocused == focused) return;
                _isFocused = focused;
                this.Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float scale = this.DeviceDpi / 96f;
                var rect = new RectangleF(0.5f, 0.5f, Math.Max(1f, this.Width - 1.5f), Math.Max(1f, this.Height - 1.5f));
                float radius = 10f * scale;

                using var path = CreateRoundedRect(rect, radius);
                using var bg = new SolidBrush(Color.FromArgb(245, 247, 250));
                using var pen = new Pen(_isFocused ? Color.FromArgb(0, 102, 204) : Color.FromArgb(208, 219, 232), _isFocused ? 1.4f : 1f);
                g.FillPath(bg, path);
                g.DrawPath(pen, path);
            }
        }

        private sealed class UserMessageBubble : Panel
        {
            private readonly Label _lblText;
            private readonly string _text;

            public UserMessageBubble(string text)
            {
                _text = text ?? string.Empty;
                this.DoubleBuffered = true;
                this.ResizeRedraw = true;
                this.BackColor = Color.White;

                _lblText = new Label
                {
                    Text = _text,
                    AutoSize = false,
                    BackColor = Color.Transparent,
                    ForeColor = Color.FromArgb(26, 33, 45),
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
                };

                var ctxMenu = new ContextMenuStrip();
                var copyItem = new ToolStripMenuItem("Copy");
                copyItem.Click += (s, e) =>
                {
                    try { Clipboard.SetText(_text); } catch { }
                };
                ctxMenu.Items.Add(copyItem);
                _lblText.ContextMenuStrip = ctxMenu;
                this.ContextMenuStrip = ctxMenu;

                this.Controls.Add(_lblText);
            }

            public void UpdateSizeForMaxWidth(int maxBubbleWidth)
            {
                float scale = this.DeviceDpi / 96f;
                int padX = (int)Math.Round(13 * scale);
                int padY = (int)Math.Round(9 * scale);
                int maxTextW = Math.Max(60, maxBubbleWidth - (padX * 2));

                Size measured = TextRenderer.MeasureText(
                    _text,
                    _lblText.Font,
                    new Size(maxTextW, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

                int boxW = Math.Min(maxBubbleWidth, Math.Max((int)(48 * scale), measured.Width + (padX * 2) + 2));
                int boxH = Math.Max((int)(32 * scale), measured.Height + (padY * 2));

                this.Size = new Size(boxW, boxH);
                _lblText.SetBounds(padX, padY, Math.Max(10, boxW - (padX * 2)), Math.Max(10, boxH - (padY * 2)));
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float scale = this.DeviceDpi / 96f;
                var rect = new RectangleF(0.5f, 0.5f, Math.Max(1f, this.Width - 1.5f), Math.Max(1f, this.Height - 1.5f));
                float radius = 10f * scale;

                using var path = CreateRoundedRect(rect, radius);
                using var fill = new SolidBrush(Color.FromArgb(238, 244, 251));
                using var border = new Pen(Color.FromArgb(213, 226, 241), 1f);
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
        }

        private sealed class FlatIconButton : Control
        {
            public enum IconKind { Retry, Copy }

            private readonly IconKind _kind;
            private bool _isHovered;
            private bool _isPressed;
            private bool _isCopied;

            public FlatIconButton(IconKind kind)
            {
                _kind = kind;
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.SupportsTransparentBackColor,
                    true);

                this.BackColor = Color.Transparent;
                this.Cursor = Cursors.Hand;
            }

            public void ShowCopiedFlash(bool copied)
            {
                if (_isCopied == copied) return;
                _isCopied = copied;
                this.Invalidate();
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                _isHovered = true;
                this.Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _isHovered = false;
                _isPressed = false;
                this.Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Left && this.Enabled)
                {
                    _isPressed = true;
                    this.Invalidate();
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                _isPressed = false;
                this.Invalidate();
            }

            protected override void OnEnabledChanged(EventArgs e)
            {
                base.OnEnabledChanged(e);
                this.Cursor = this.Enabled ? Cursors.Hand : Cursors.Default;
                this.Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float scale = this.DeviceDpi / 96f;

                if (_isHovered && this.Enabled)
                {
                    var bgRect = new RectangleF(0.5f, 0.5f, Math.Max(1f, this.Width - 1.5f), Math.Max(1f, this.Height - 1.5f));
                    using var path = CreateRoundedRect(bgRect, 5f * scale);
                    using var bg = new SolidBrush(_isPressed ? Color.FromArgb(224, 232, 242) : Color.FromArgb(238, 243, 249));
                    g.FillPath(bg, path);
                }

                Color iconColor = !this.Enabled
                    ? Color.FromArgb(180, 186, 196)
                    : (_isCopied ? Color.FromArgb(22, 163, 74) : (_isHovered ? Color.FromArgb(30, 41, 59) : Color.FromArgb(95, 105, 120)));

                float cx = this.Width / 2f;
                float cy = this.Height / 2f;

                using var pen = new Pen(iconColor, 1.5f * scale)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };

                if (_kind == IconKind.Retry)
                {
                    float r = 5.2f * scale;
                    var arcRect = new RectangleF(cx - r, cy - r, r * 2f, r * 2f);
                    g.DrawArc(pen, arcRect, -40f, 290f);

                    float tipX = cx + r * 0.78f;
                    float tipY = cy - r * 0.62f;
                    g.DrawLine(pen, tipX - 3.2f * scale, tipY - 0.5f * scale, tipX, tipY);
                    g.DrawLine(pen, tipX + 0.3f * scale, tipY - 3.2f * scale, tipX, tipY);
                }
                else if (_isCopied)
                {
                    g.DrawLine(pen, cx - 4.5f * scale, cy + 0.5f * scale, cx - 1.2f * scale, cy + 3.8f * scale);
                    g.DrawLine(pen, cx - 1.2f * scale, cy + 3.8f * scale, cx + 5f * scale, cy - 3.2f * scale);
                }
                else
                {
                    float s = 4.2f * scale;
                    // Back square
                    g.DrawRectangle(pen, cx - s + 1.4f * scale, cy - s - 1.2f * scale, s * 1.65f, s * 1.65f);
                    // Front square filled lightly with background to mask back edge cleanly
                    var front = new RectangleF(cx - s - 1.2f * scale, cy - s + 1.4f * scale, s * 1.65f, s * 1.65f);
                    using var maskBrush = new SolidBrush(_isHovered ? Color.FromArgb(238, 243, 249) : Color.White);
                    g.FillRectangle(maskBrush, front);
                    g.DrawRectangle(pen, front.X, front.Y, front.Width, front.Height);
                }
            }
        }

        private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Max(1f, Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height)));
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        #endregion
    }
}
