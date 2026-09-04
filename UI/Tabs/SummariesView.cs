using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using KerkenezMail.Languages;
using KerkenezMail.Models;
using KerkenezMail.Services;

namespace KerkenezMail.UI.Tabs
{
    public class SummariesView : UserControl
    {
        private readonly ConfigService _configService;
        private readonly ImapService _imapService;
        private readonly LlamaServerManager _llamaManager;
        private readonly LlmSummarizerService _llmService;
        private readonly IProgress<string> _logger;

        private CancellationTokenSource? _cts;
        private volatile bool _isBatchSyncing = false;
        private MailFolderType _currentFolder = MailFolderType.Inbox;
        private readonly Dictionary<MailFolderType, List<EmailItem>> _folderStorage = new()
        {
            { MailFolderType.Inbox, new List<EmailItem>() },
            { MailFolderType.Sent, new List<EmailItem>() },
            { MailFolderType.Archive, new List<EmailItem>() },
            { MailFolderType.Spam, new List<EmailItem>() },
            { MailFolderType.Trash, new List<EmailItem>() }
        };
        private readonly Dictionary<MailFolderType, bool> _folderFetchedOnce = new()
        {
            { MailFolderType.Inbox, false },
            { MailFolderType.Sent, false },
            { MailFolderType.Archive, false },
            { MailFolderType.Spam, false },
            { MailFolderType.Trash, false }
        };
        private readonly List<EmailItem> _emails = new List<EmailItem>();
        private readonly List<EmailItem> _selectedEmailsOrder = new List<EmailItem>();

        // Triage State & Debouncing
        private enum TriageActionType { Archive, Delete, MoveToInbox }
        private class PendingTriageItem
        {
            public EmailItem Email { get; set; } = null!;
            public TriageActionType Action { get; set; }
            public MailFolderType SourceFolder { get; set; } = MailFolderType.Inbox;
        }
        private readonly List<PendingTriageItem> _pendingSingleTriage = new List<PendingTriageItem>();
        private readonly object _triageLock = new object();
        private System.Threading.Timer? _debounceTriageTimer;
        private readonly List<Task> _inFlightTasks = new List<Task>();

        // Controls
        private Panel _topPanel = null!;
        private Button _btnRefresh = null!;
        private Button _btnCopySummary = null!;
        private Button _btnExport = null!;
        private Button _btnOpenInBrowser = null!;
        private Button _btnArchive = null!;
        private Button _btnDelete = null!;
        private ToolTip _topBarToolTip = null!;
        private ComboBox _cboAccountFilter = null!;
        private TextBox _txtSearch = null!;
        private ListView _lvEmails = null!;
        private TextBox _txtSummary = null!;
        private RichTextBox _rtbEmailBody = null!;
        private Label _lblEmailMeta = null!;
        private Label _lblEmailSubject = null!;
        private Panel _pnlSubjectViewport = null!;
        private HScrollBar _sliderSubject = null!;
        private Panel _pnlReplyBox = null!;
        private Button _btnReply = null!;
        private Button _btnMoveToInbox = null!;
        private FlowLayoutPanel _pnlAttachments = null!;
        private Label _lblAttachmentsTitle = null!;
        private Label _lblInboxHeader = null!;
        private Label _lblAccount = null!;
        private Label _lblSummaryTitle = null!;
        private Label _lblSummaryHint = null!;
        private ProgressBar _progressBar = null!;
        private SplitContainer _mainSplit = null!;
        private SplitContainer _middleSplit = null!;
        private int _inboxPanelWidth = 440;
        private bool _isInitialSplitSet = false;

        // Inbox Cell Hover ToolTip
        private ToolTip _inboxCellToolTip = null!;
        private System.Windows.Forms.Timer? _inboxHoverTimer;
        private ListViewItem? _lastHoverItem;
        private int _lastHoverColumn = -1;
        private ListViewItem? _lastActiveTooltipItem;
        private bool _isToolTipActive = false;
        private DateTime _lastToolTipShownTime = DateTime.MinValue;

        // Link Hover ToolTip
        private readonly List<(int Start, int Length, string Url)> _currentEmailLinkSpans = new List<(int Start, int Length, string Url)>();
        private string? _lastHoverLinkUrl;
        private System.Windows.Forms.Timer? _linkHoverTimer;
        private readonly SemaphoreSlim _summaryQueueSemaphore = new SemaphoreSlim(1, 1);

        public event Action<string, string>? StatusUpdated;
        public event EventHandler<EmailItem>? ReplyRequested;

        public SummariesView(
            ConfigService configService,
            ImapService imapService,
            LlamaServerManager llamaManager,
            LlmSummarizerService llmService,
            IProgress<string> logger)
        {
            _configService = configService;
            _imapService = imapService;
            _llamaManager = llamaManager;
            _llmService = llmService;
            _logger = logger;

            InitializeComponent();
            RefreshAccountFilter();
            _configService.SettingsChanged += RefreshAccountFilter;
            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLocalization();
            ApplyLocalization();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            float scale = this.DeviceDpi / 96f;

            // 1. Top Action Toolbar - Mathematically calculated equal top/bottom spacing
            int topPad = (int)(16 * scale);
            int btnH = (int)(34 * scale);
            int totalTopH = (topPad * 2) + btnH + 2;

            _topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = totalTopH,
                Padding = new Padding((int)(16 * scale), topPad, (int)(16 * scale), topPad),
                BackColor = Color.White
            };

            _topPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawLine(p, 0, _topPanel.Height - 1, _topPanel.Width, _topPanel.Height - 1);
            };

            var actionsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0)
            };

            _btnRefresh = new Button
            {
                Text = "🔄 " + Lang.T(StringKeys.InboxRefresh),
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(7 * scale), (int)(5 * scale), (int)(7 * scale), (int)(5 * scale)),
                Margin = new Padding(0, 0, (int)(4 * scale), 0),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnRefresh.Click += async (s, e) => await FetchAndAutoSummarizeAsync();

            _btnCopySummary = new Button
            {
                Text = "📋 " + Lang.T(StringKeys.InboxCopySummary),
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(7 * scale), (int)(5 * scale), (int)(7 * scale), (int)(5 * scale)),
                Margin = new Padding(0, 0, (int)(4 * scale), 0),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _btnCopySummary.Click += OnCopySummaryClick;

            _btnExport = new Button
            {
                Text = "💾 " + Lang.T(StringKeys.InboxExport),
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(7 * scale), (int)(5 * scale), (int)(7 * scale), (int)(5 * scale)),
                Margin = new Padding(0, 0, (int)(4 * scale), 0),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            _btnExport.Click += OnExportClick;

            // Square Open in Browser button (exact same height as full buttons, fits to the left of Archive/Delete)
            string topBarIconFont = GetTopBarIconFontFamily();
            _btnOpenInBrowser = new Button
            {
                Width = btnH,
                Height = btnH,
                Margin = new Padding(0, 0, (int)(4 * scale), 0),
                Padding = new Padding(0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font(topBarIconFont, 11F, FontStyle.Regular),
                Text = topBarIconFont.Contains("Segoe") ? "\uE774" : "🌐"
            };
            _btnOpenInBrowser.Click += OnOpenInBrowserClick;

            // Stacked Archive and Delete half-height square buttons
            var pnlTriageButtons = new Panel
            {
                Width = (int)(32 * scale),
                Height = btnH,
                Margin = new Padding(0, 0, (int)(10 * scale), 0),
                Padding = new Padding(0)
            };

            int halfBtnH = (btnH - 2) / 2;

            _btnArchive = new Button
            {
                Dock = DockStyle.Top,
                Height = halfBtnH,
                Text = "📥",
                Font = new Font("Segoe UI Emoji", 7F, FontStyle.Regular),
                Padding = new Padding(0),
                Margin = new Padding(0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnArchive.Click += OnArchiveClick;

            _btnDelete = new Button
            {
                Dock = DockStyle.Bottom,
                Height = halfBtnH,
                Text = "🗑",
                Font = new Font("Segoe UI Emoji", 7F, FontStyle.Regular),
                Padding = new Padding(0),
                Margin = new Padding(0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnDelete.Click += OnDeleteClick;

            _topBarToolTip = new ToolTip
            {
                AutoPopDelay = 8000,
                InitialDelay = 400,
                ReshowDelay = 150
            };
            _topBarToolTip.SetToolTip(_btnRefresh, Lang.T(StringKeys.InboxTipRefresh));
            _topBarToolTip.SetToolTip(_btnCopySummary, Lang.T(StringKeys.InboxTipCopySummary));
            _topBarToolTip.SetToolTip(_btnExport, Lang.T(StringKeys.InboxTipExport));
            _topBarToolTip.SetToolTip(_btnOpenInBrowser, Lang.T(StringKeys.InboxTipOpenInBrowser));
            _topBarToolTip.SetToolTip(_btnArchive, Lang.T(StringKeys.InboxTipArchive));
            _topBarToolTip.SetToolTip(_btnDelete, Lang.T(StringKeys.InboxTipDelete));

            pnlTriageButtons.Controls.Add(_btnDelete);
            pnlTriageButtons.Controls.Add(_btnArchive);

            _lblAccount = new Label
            {
                Text = Lang.T(StringKeys.InboxAccountLabel),
                AutoSize = true,
                Margin = new Padding(0, (int)(7 * scale), (int)(6 * scale), 0),
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            _cboAccountFilter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = (int)(150 * scale),
                Height = (int)(28 * scale),
                Margin = new Padding(0, (int)(3 * scale), (int)(10 * scale), 0),
                Font = new Font("Segoe UI", 9F)
            };
            _cboAccountFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

            _txtSearch = new TextBox
            {
                Width = (int)(180 * scale),
                Height = (int)(28 * scale),
                Margin = new Padding(0, (int)(3 * scale), 0, 0),
                Font = new Font("Segoe UI", 9F),
                PlaceholderText = Lang.T(StringKeys.InboxSearchPlaceholder)
            };
            _txtSearch.TextChanged += (s, e) => ApplyFilter();

            actionsFlow.Controls.Add(_btnRefresh);
            actionsFlow.Controls.Add(_btnCopySummary);
            actionsFlow.Controls.Add(_btnExport);
            actionsFlow.Controls.Add(_btnOpenInBrowser);
            actionsFlow.Controls.Add(pnlTriageButtons);
            actionsFlow.Controls.Add(_lblAccount);
            actionsFlow.Controls.Add(_cboAccountFilter);
            actionsFlow.Controls.Add(_txtSearch);

            _progressBar = new ProgressBar
            {
                Height = Math.Max(3, (int)(3 * scale)),
                Visible = false,
                Style = ProgressBarStyle.Marquee
            };

            void UpdateProgressBarBounds()
            {
                int barH = Math.Max(3, (int)(3 * scale));
                int btnBottom = topPad + btnH;
                int bottomBorderY = _topPanel.Height - 1;
                int gap = bottomBorderY - btnBottom;
                int barY = btnBottom + Math.Max(0, (gap - barH) / 2);

                int startX = actionsFlow.Left + _btnRefresh.Left;
                int endX = actionsFlow.Left + _txtSearch.Right;
                int barW = Math.Max(50, endX - startX);

                _progressBar.SetBounds(startX, barY, barW, barH);
                _progressBar.BringToFront();
            }

            _topPanel.Resize += (s, e) => UpdateProgressBarBounds();
            _topPanel.Layout += (s, e) => UpdateProgressBarBounds();
            actionsFlow.Layout += (s, e) => UpdateProgressBarBounds();

            _topPanel.Controls.Add(_progressBar);
            _topPanel.Controls.Add(actionsFlow);
            UpdateProgressBarBounds();

            // 2. Main SplitContainer (Left/Middle = Email Body + AI Summary, Right = Grouped Inbox List)
            _mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel2,
                SplitterWidth = 6,
                Padding = new Padding(14, 12, 14, 12)
            };

            _mainSplit.SplitterMoved += (s, e) =>
            {
                if (_mainSplit.Width > 0 && _isInitialSplitSet)
                {
                    _inboxPanelWidth = Math.Max(260, _mainSplit.Width - _mainSplit.SplitterDistance);
                }
            };

            // LEFT/MIDDLE: Email Body (Top) and AI Summary (Bottom)
            _middleSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 420,
                SplitterWidth = 6
            };

            // --- Middle-Top: The Email Content (Clean Body) ---
            var emailViewerPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(14)
            };

            var metaHeader = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 0, 0, 10)
            };

            var subjectRow = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 0, 0, 4)
            };

            _pnlReplyBox = new Panel
            {
                Dock = DockStyle.Right,
                Width = (int)(88 * scale),
                Padding = new Padding((int)(6 * scale), 0, 0, 0)
            };

            _btnReply = new Button
            {
                Text = "↩  " + Lang.T(StringKeys.InboxBtnReply),
                Dock = DockStyle.Top,
                Height = (int)(26 * scale),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Visible = false
            };
            _btnReply.Click += (s, e) =>
            {
                var currentEmail = GetCurrentPreviewEmail();
                if (currentEmail != null)
                {
                    ReplyRequested?.Invoke(this, currentEmail);
                }
            };
            _topBarToolTip.SetToolTip(_btnReply, Lang.T(StringKeys.InboxTipReply));

            _btnMoveToInbox = new Button
            {
                Text = "📥 " + Lang.T(StringKeys.InboxBtnMoveInbox),
                Dock = DockStyle.Top,
                Height = (int)(26 * scale),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Visible = false
            };
            _btnMoveToInbox.Click += OnMoveToInboxClick;
            _topBarToolTip.SetToolTip(_btnMoveToInbox, Lang.T(StringKeys.InboxTipMoveToInbox));

            _pnlReplyBox.Controls.Add(_btnMoveToInbox);
            _pnlReplyBox.Controls.Add(_btnReply);

            var pnlSubjectContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            _pnlSubjectViewport = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(26 * scale),
                AutoScroll = false
            };

            _lblEmailSubject = new Label
            {
                Text = $"{Lang.T(StringKeys.InboxSubjectPrefix)} {Lang.T(StringKeys.InboxNoEmailSelected)}",
                AutoSize = true,
                Location = new Point(0, (int)(2 * scale)),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 20, 20)
            };

            _pnlSubjectViewport.Controls.Add(_lblEmailSubject);

            _sliderSubject = new HScrollBar
            {
                Dock = DockStyle.Top,
                Height = (int)(13 * scale),
                Visible = false,
                Cursor = Cursors.Hand
            };

            _sliderSubject.Scroll += (s, e) =>
            {
                _lblEmailSubject.Location = new Point(-_sliderSubject.Value, (int)(2 * scale));
            };

            void onSubjectWheel(object? s, MouseEventArgs e)
            {
                if (_sliderSubject.Visible)
                {
                    int step = (int)(24 * scale);
                    int delta = e.Delta > 0 ? -step : step;
                    int newVal = Math.Max(0, Math.Min(_sliderSubject.Maximum - _sliderSubject.LargeChange + 1, _sliderSubject.Value + delta));
                    if (newVal != _sliderSubject.Value)
                    {
                        _sliderSubject.Value = newVal;
                        _lblEmailSubject.Location = new Point(-newVal, (int)(2 * scale));
                    }
                }
            }
            _pnlSubjectViewport.MouseWheel += onSubjectWheel;
            _lblEmailSubject.MouseWheel += onSubjectWheel;

            _pnlSubjectViewport.Resize += (s, e) => UpdateSubjectSlider();

            pnlSubjectContainer.Controls.Add(_sliderSubject);
            pnlSubjectContainer.Controls.Add(_pnlSubjectViewport);

            subjectRow.Controls.Add(pnlSubjectContainer);
            subjectRow.Controls.Add(_pnlReplyBox);

            _lblEmailMeta = new Label
            {
                Text = $"{Lang.T(StringKeys.InboxDetailFrom)} -   •   {Lang.T(StringKeys.InboxDetailDate)} -   •   {Lang.T(StringKeys.InboxDetailAccount)} -",
                Dock = DockStyle.Top,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(100, 100, 100)
            };

            metaHeader.Controls.Add(_lblEmailMeta);
            metaHeader.Controls.Add(subjectRow);

            _rtbEmailBody = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                DetectUrls = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.75F)
            };
            _rtbEmailBody.LinkClicked += OnEmailLinkClicked;
            _rtbEmailBody.MouseMove += OnEmailBodyMouseMove;
            _rtbEmailBody.MouseLeave += OnEmailBodyMouseLeave;
            _rtbEmailBody.MouseDown += (s, e) => ResetLinkToolTip();

            _linkHoverTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _linkHoverTimer.Tick += OnLinkHoverTimerTick;

            // --- Attachment Bar at the bottom of Email Viewer Panel (Compact Single-Row Strip) ---
            _pnlAttachments = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.FromArgb(246, 249, 253),
                Padding = new Padding((int)(10 * scale), (int)(3 * scale), (int)(10 * scale), (int)(4 * scale)),
                WrapContents = true,
                Visible = false
            };
            _pnlAttachments.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(220, 228, 238), 1);
                e.Graphics.DrawLine(p, 0, 0, _pnlAttachments.Width, 0);
            };

            _lblAttachmentsTitle = new Label
            {
                Text = "📎 " + Lang.T(StringKeys.InboxAttachmentsTitle),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 55, 80),
                Margin = new Padding(0, (int)(4 * scale), (int)(8 * scale), 0)
            };
            _pnlAttachments.Controls.Add(_lblAttachmentsTitle);

            emailViewerPanel.Controls.Add(_rtbEmailBody);
            emailViewerPanel.Controls.Add(_pnlAttachments);
            emailViewerPanel.Controls.Add(metaHeader);
            _middleSplit.Panel1.Controls.Add(emailViewerPanel);

            // --- Middle-Bottom: AI Executive Summary ---
            var summaryCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(245, 250, 255),
                Padding = new Padding(12)
            };

            summaryCard.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(180, 215, 250), 1);
                e.Graphics.DrawRectangle(p, 0, 0, summaryCard.Width - 1, summaryCard.Height - 1);
            };

            var summaryHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 26,
                Padding = new Padding(0, 0, 0, 4)
            };

            _lblSummaryTitle = new Label
            {
                Text = "✨ " + Lang.T(StringKeys.InboxAiExecutiveSummary),
                Dock = DockStyle.Left,
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204)
            };

            _lblSummaryHint = new Label
            {
                Text = Lang.T(StringKeys.InboxAiGeneratedVram),
                Dock = DockStyle.Right,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(120, 120, 120)
            };

            summaryHeader.Controls.Add(_lblSummaryTitle);
            summaryHeader.Controls.Add(_lblSummaryHint);

            _txtSummary = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(245, 250, 255),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                PlaceholderText = Lang.T(StringKeys.InboxAiSummaryPlaceholder)
            };

            summaryCard.Controls.Add(_txtSummary);
            summaryCard.Controls.Add(summaryHeader);
            _middleSplit.Panel2.Controls.Add(summaryCard);

            _mainSplit.Panel1.Controls.Add(_middleSplit);

            // --- RIGHT PANEL: Grouped Inbox Email List ---
            var rightListPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(0)
            };

            _lblInboxHeader = new Label
            {
                Text = "Inbox (0 emails)",
                Dock = DockStyle.Top,
                Height = 32,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            _lvEmails = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F),
                ShowGroups = true,
                MultiSelect = true
            };
            SetupListViewColumns();
            _lvEmails.ItemSelectionChanged += OnEmailItemSelectionChanged;
            _lvEmails.SelectedIndexChanged += OnEmailSelected;

            _inboxCellToolTip = new ToolTip
            {
                ShowAlways = true,
                UseAnimation = true,
                UseFading = true
            };

            _inboxHoverTimer = new System.Windows.Forms.Timer();
            _inboxHoverTimer.Tick += OnInboxHoverTimerTick;

            _lvEmails.MouseMove += OnListViewMouseMove;
            _lvEmails.MouseLeave += OnListViewMouseLeave;
            _lvEmails.MouseDown += (s, e) => ResetInboxToolTip();
            _lvEmails.MouseWheel += (s, e) => ResetInboxToolTip();
            _lvEmails.KeyDown += (s, e) => ResetInboxToolTip();

            rightListPanel.Controls.Add(_lvEmails);
            rightListPanel.Controls.Add(_lblInboxHeader);
            _mainSplit.Panel2.Controls.Add(rightListPanel);

            this.Controls.Add(_mainSplit);
            this.Controls.Add(_topPanel);

            this.Load += (s, e) => 
            {
                ApplyAiModeLayout();
                AdjustSplitter();
            };
            this.Resize += (s, e) => AdjustSplitter();
        }

        public void SetupListViewColumns()
        {
            _lvEmails.BeginUpdate();
            _lvEmails.Columns.Clear();
            if (!_configService.Settings.IsAiDisabled)
            {
                _lvEmails.Columns.Add("⚡", 44);
                _lvEmails.Columns.Add(Lang.T(StringKeys.InboxColSubject), 190);
            }
            else
            {
                _lvEmails.Columns.Add(Lang.T(StringKeys.InboxColSubject), 234);
            }
            _lvEmails.Columns.Add(Lang.T(StringKeys.InboxColAccount), 95);
            _lvEmails.Columns.Add(Lang.T(StringKeys.InboxColFrom), 120);
            _lvEmails.Columns.Add(Lang.T(StringKeys.InboxColDate), 85);
            _lvEmails.EndUpdate();
        }

        public void ApplyLocalization()
        {
            if (this.IsDisposed) return;
            if (_btnRefresh != null)
            {
                _btnRefresh.Text = (_currentFolder == MailFolderType.Inbox)
                    ? "🔄 " + Lang.T(StringKeys.InboxRefresh)
                    : $"🔄 {Lang.T(StringKeys.InboxRefresh)} {_currentFolder.GetDisplayName()}";
            }
            if (_btnCopySummary != null) _btnCopySummary.Text = "📋 " + Lang.T(StringKeys.InboxCopySummary);
            if (_btnExport != null) _btnExport.Text = "💾 " + Lang.T(StringKeys.InboxExport);
            if (_btnReply != null) _btnReply.Text = "↩  " + Lang.T(StringKeys.InboxBtnReply);
            if (_btnMoveToInbox != null) _btnMoveToInbox.Text = "📥 " + Lang.T(StringKeys.InboxBtnMoveInbox);
            if (_btnDelete != null) _btnDelete.Text = "🗑";
            if (_btnArchive != null) _btnArchive.Text = "📥";
            if (_txtSearch != null) _txtSearch.PlaceholderText = Lang.T(StringKeys.InboxSearchPlaceholder);
            if (_lblAccount != null) _lblAccount.Text = Lang.T(StringKeys.InboxAccountLabel);
            if (_lblSummaryTitle != null) _lblSummaryTitle.Text = "✨ " + Lang.T(StringKeys.InboxAiExecutiveSummary);
            if (_lblSummaryHint != null) _lblSummaryHint.Text = Lang.T(StringKeys.InboxAiGeneratedVram);
            if (_txtSummary != null) _txtSummary.PlaceholderText = Lang.T(StringKeys.InboxAiSummaryPlaceholder);
            if (_lblAttachmentsTitle != null) _lblAttachmentsTitle.Text = "📎 " + Lang.T(StringKeys.InboxAttachmentsTitle);

            if (_topBarToolTip != null)
            {
                if (_btnRefresh != null)
                {
                    _topBarToolTip.SetToolTip(_btnRefresh, (_currentFolder == MailFolderType.Inbox)
                        ? Lang.T(StringKeys.InboxTipRefresh)
                        : $"{Lang.T(StringKeys.InboxRefresh)} {_currentFolder.GetDisplayName()}");
                }
                if (_btnCopySummary != null) _topBarToolTip.SetToolTip(_btnCopySummary, Lang.T(StringKeys.InboxTipCopySummary));
                if (_btnExport != null) _topBarToolTip.SetToolTip(_btnExport, Lang.T(StringKeys.InboxTipExport));
                if (_btnOpenInBrowser != null) _topBarToolTip.SetToolTip(_btnOpenInBrowser, Lang.T(StringKeys.InboxTipOpenInBrowser));
                if (_btnArchive != null) _topBarToolTip.SetToolTip(_btnArchive, Lang.T(StringKeys.InboxTipArchive));
                if (_btnDelete != null)
                {
                    _topBarToolTip.SetToolTip(_btnDelete, _currentFolder == MailFolderType.Trash 
                        ? Lang.T(StringKeys.InboxDeletePermanentlyTip) 
                        : Lang.T(StringKeys.InboxDeleteMoveTip));
                }
                if (_btnReply != null) _topBarToolTip.SetToolTip(_btnReply, Lang.T(StringKeys.InboxTipReply));
                if (_btnMoveToInbox != null) _topBarToolTip.SetToolTip(_btnMoveToInbox, Lang.T(StringKeys.InboxTipMoveToInbox));
            }

            SetupListViewColumns();
            RefreshAccountFilter();
            UpdateHeaderTitle(_emails.Count, _emails.Count(e => !e.IsRead));

            if (_selectedEmailsOrder.Count == 0 || _lvEmails.SelectedItems.Count == 0)
            {
                ResetEmailPreview();
            }
            else
            {
                var curr = GetCurrentPreviewEmail();
                if (curr != null) DisplayEmail(curr);
            }
        }

        public void ApplyAiModeLayout()
        {
            if (this.IsDisposed) return;

            bool isAiDisabled = _configService.Settings.IsAiDisabled;

            if (isAiDisabled)
            {
                CancelRunningOperations();
                _llamaManager.Stop(_logger);
            }

            var current = GetCurrentPreviewEmail();
            UpdateSummaryPaneVisibility(current);

            SetupListViewColumns();

            if (this.IsHandleCreated)
            {
                PopulateListView();

                if (current != null)
                {
                    DisplayEmail(current);
                }
            }
        }

        private void UpdateSummaryPaneVisibility(EmailItem? email)
        {
            if (_middleSplit == null) return;

            bool isAiDisabled = _configService.Settings.IsAiDisabled;
            if (!isAiDisabled)
            {
                // In AI mode, the summary pane is always visible
                _middleSplit.Panel2Collapsed = false;
                return;
            }

            // In No-AI / Battery Saver mode:
            // If the user is viewing an email that already has a generated summary,
            // leave the summary card visible. Only collapse if there is no pre-existing summary.
            bool hasValidSummary = email != null && 
                                   !string.IsNullOrWhiteSpace(email.Summary) && 
                                   !email.Summary.StartsWith("✨", StringComparison.OrdinalIgnoreCase) &&
                                   !email.Summary.StartsWith("(LLM Error", StringComparison.OrdinalIgnoreCase) &&
                                   !email.Summary.StartsWith("(Could not reach", StringComparison.OrdinalIgnoreCase);

            _middleSplit.Panel2Collapsed = !hasValidSummary;
        }

        private void AdjustSplitter()
        {
            if (_mainSplit.Width > 400)
            {
                float scale = this.DeviceDpi / 96f;
                if (!_isInitialSplitSet)
                {
                    _inboxPanelWidth = Math.Min((int)(440 * scale), Math.Max((int)(310 * scale), (int)(_mainSplit.Width * 0.28)));
                    _isInitialSplitSet = true;
                }

                int targetDistance = _mainSplit.Width - _inboxPanelWidth;
                int minLeft = Math.Min((int)(280 * scale), _mainSplit.Width / 2);
                int maxDistance = _mainSplit.Width - (int)(220 * scale);

                if (targetDistance > minLeft && targetDistance < maxDistance)
                {
                    _mainSplit.SplitterDistance = targetDistance;
                }
                else if (targetDistance <= minLeft && _mainSplit.Width > 350)
                {
                    _mainSplit.SplitterDistance = minLeft;
                }
            }
        }

        public void RefreshAccountFilter()
        {
            string? currentSelection = _cboAccountFilter.SelectedItem?.ToString();
            int prevIndex = _cboAccountFilter.SelectedIndex;

            _cboAccountFilter.Items.Clear();
            _cboAccountFilter.Items.Add(Lang.T(StringKeys.InboxAllAccounts));

            foreach (var acc in _configService.GetAccounts())
            {
                _cboAccountFilter.Items.Add(acc.Name);
            }

            if (!string.IsNullOrEmpty(currentSelection) && _cboAccountFilter.Items.Contains(currentSelection))
            {
                _cboAccountFilter.SelectedItem = currentSelection;
            }
            else if (prevIndex >= 0 && prevIndex < _cboAccountFilter.Items.Count)
            {
                _cboAccountFilter.SelectedIndex = prevIndex;
            }
            else
            {
                _cboAccountFilter.SelectedIndex = 0;
            }
        }

        public async Task SwitchToFolderAsync(MailFolderType folder, bool forceRefresh = false)
        {
            if (_currentFolder == folder && !forceRefresh && _emails.Count > 0)
            {
                return;
            }

            // 1. Cancel any active folder sync immediately to stop previous folder network traffic
            try
            {
                _cts?.Cancel();
            }
            catch { }

            _isBatchSyncing = false;
            _currentFolder = folder;

            // 2. Update UI controls for this folder
            _btnRefresh.Enabled = true;
            _progressBar.Visible = false;
            _btnRefresh.Text = (folder == MailFolderType.Inbox)
                ? "🔄 " + Lang.T(StringKeys.InboxRefresh)
                : $"🔄 {Lang.T(StringKeys.InboxRefresh)} {folder.GetDisplayName()}";

            _topBarToolTip.SetToolTip(_btnRefresh, $"{Lang.T(StringKeys.InboxRefresh)} {folder.GetDisplayName()}");

            if (folder == MailFolderType.Trash)
            {
                _topBarToolTip.SetToolTip(_btnDelete, Lang.T(StringKeys.InboxDeletePermanentlyTip));
                _btnArchive.Enabled = false;
            }
            else if (folder == MailFolderType.Archive)
            {
                _topBarToolTip.SetToolTip(_btnDelete, Lang.T(StringKeys.InboxDeleteMoveTip));
                _btnArchive.Enabled = false;
            }
            else
            {
                _topBarToolTip.SetToolTip(_btnDelete, Lang.T(StringKeys.InboxDeleteMoveTip));
                _btnArchive.Enabled = true;
            }

            // 3. Load exclusively from this folder's dedicated storage
            lock (_folderStorage)
            {
                _emails.Clear();
                _emails.AddRange(_folderStorage[folder]);
            }

            PopulateListView();

            int unreadCount = _emails.Count(e => !e.IsRead);
            string status;
            if (_configService.Settings.IsBatterySaverActive)
            {
                status = Lang.T(StringKeys.StatusBatterySaverNoAi);
            }
            else if (_configService.Settings.IsAiDisabled)
            {
                status = Lang.T(StringKeys.StatusAiDisabled);
            }
            else
            {
                string backendType = _configService.Settings.AiBackend;
                status = string.Equals(backendType, "LlamaCpp", StringComparison.OrdinalIgnoreCase) 
                    ? (_configService.Settings.InstantVramUnload ? Lang.T(StringKeys.StatusVramFree) : Lang.T(StringKeys.StatusModelLoaded)) 
                    : (string.Equals(backendType, "Ollama", StringComparison.OrdinalIgnoreCase) ? Lang.T(StringKeys.StatusOllamaActive) : Lang.T(StringKeys.StatusCloudActive));
            }

            string metric = folder == MailFolderType.Inbox
                ? Lang.Format(StringKeys.StatusReadyEmails, _emails.Count, unreadCount)
                : Lang.Format(StringKeys.StatusReadyFolder, _emails.Count, folder.GetDisplayName());
            StatusUpdated?.Invoke(metric, status);

            if (_lvEmails.Items.Count > 0)
            {
                _lvEmails.Items[0].Selected = true;
            }
            else
            {
                ResetEmailPreview();
            }
            UpdateActionButtonsVisibility();

            // 4. If this folder was never fetched from IMAP yet or forceRefresh is true, fetch it now!
            if (!_folderFetchedOnce[folder] || forceRefresh)
            {
                await FetchAndAutoSummarizeAsync(folder);
            }
        }

        public async Task FetchAndAutoSummarizeAsync(MailFolderType? targetFolder = null)
        {
            var folderToFetch = targetFolder ?? _currentFolder;

            // Cancel any previous sync before starting a new one
            try
            {
                _cts?.Cancel();
            }
            catch { }

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            _isBatchSyncing = true;
            _btnRefresh.Enabled = false;
            _progressBar.Visible = true;

            // Reset this folder's dedicated storage for a clean fresh fetch
            lock (_folderStorage)
            {
                _folderStorage[folderToFetch].Clear();
            }

            if (_currentFolder == folderToFetch)
            {
                _emails.Clear();
                PopulateListView();
                ResetEmailPreview();
            }

            var settings = _configService.Settings;
            var accounts = _configService.GetAccounts();
            if (accounts.Count == 0)
            {
                _logger.Report("\r\n" + new string('═', 60));
                _logger.Report("[!] No email accounts configured. Please add an account in the Accounts tab.");
                StatusUpdated?.Invoke(Lang.T(StringKeys.StatusNoAccounts), Lang.T(StringKeys.StatusReady));
                _isBatchSyncing = false;
                _btnRefresh.Enabled = true;
                _progressBar.Visible = false;
                return;
            }

            string backendName = settings.GetBackendDisplayName();
            string folderName = folderToFetch.GetDisplayName();

            if (_currentFolder == folderToFetch)
            {
                StatusUpdated?.Invoke(Lang.Format(StringKeys.StatusSyncingFolder, folderName), "Active");
            }
            _logger.Report("\r\n" + new string('═', 60));
            _logger.Report($"[*] Fast-syncing all accounts for [{folderName}] with AI backend [{backendName}]...");

            try
            {
                // 1. Launch LLM Server in parallel background task ONLY if using local llama.cpp and AI is enabled
                Task<bool>? serverTask = null;
                if (!settings.IsAiDisabled && string.Equals(settings.AiBackend, "LlamaCpp", StringComparison.OrdinalIgnoreCase) && settings.AutoStartLlamaServer)
                {
                    serverTask = _llamaManager.StartAsync(
                        settings.LlamaModelPath,
                        settings.LlamaServerPort,
                        settings.LlamaGpuLayers,
                        contextSize: settings.LlamaContextSize,
                        logger: _logger,
                        ct: ct);
                }

                // 2. Concurrently fetch all IMAP accounts in parallel tasks, streaming emails to UI as they arrive!
                var activeSummaryTasks = new System.Collections.Concurrent.ConcurrentBag<Task>();

                var fetchTask = _imapService.FetchAllAccountsParallelAsync(
                    accounts,
                    settings,
                    _logger,
                    onEmailFetched: emailItem =>
                    {
                        if (ct.IsCancellationRequested) return;

                        // Ensure folder tag is accurate
                        emailItem.Folder = folderToFetch;

                        // Store in this folder's dedicated storage
                        lock (_folderStorage)
                        {
                            var targetStorage = _folderStorage[folderToFetch];
                            bool exists = emailItem.UniqueId > 0
                                ? targetStorage.Any(e => e.UniqueId == emailItem.UniqueId && string.Equals(e.AccountName, emailItem.AccountName, StringComparison.OrdinalIgnoreCase))
                                : targetStorage.Contains(emailItem);

                            if (!exists)
                            {
                                targetStorage.Add(emailItem);
                            }
                        }

                        // STRICT FOLDER ISOLATION: Only update active UI if user is STILL viewing THIS folder!
                        if (_currentFolder == folderToFetch)
                        {
                            if (this.IsDisposed || !this.IsHandleCreated) return;

                            try
                            {
                                this.BeginInvoke(new Action(() =>
                                {
                                    if (this.IsDisposed || !this.IsHandleCreated || _currentFolder != folderToFetch) return;

                                    bool alreadyInList = emailItem.UniqueId > 0
                                        ? _emails.Any(e => e.UniqueId == emailItem.UniqueId && string.Equals(e.AccountName, emailItem.AccountName, StringComparison.OrdinalIgnoreCase))
                                        : _emails.Contains(emailItem);

                                    if (!alreadyInList)
                                    {
                                        _emails.Add(emailItem);
                                        AddEmailItemToListView(emailItem);

                                        if (_lvEmails.SelectedItems.Count == 0 && _lvEmails.Items.Count > 0)
                                        {
                                            _lvEmails.Items[0].Selected = true;
                                        }
                                    }
                                }));
                            }
                            catch { }

                            if (!settings.IsAiDisabled && !emailItem.IsRead && folderToFetch == MailFolderType.Inbox)
                            {
                                activeSummaryTasks.Add(SummarizeUnreadEmailInBackgroundAsync(emailItem, settings, serverTask, ct));
                            }
                        }
                    },
                    ct: ct,
                    folderType: folderToFetch);

                await fetchTask;
                if (serverTask != null) await serverTask;

                if (!activeSummaryTasks.IsEmpty)
                {
                    await Task.WhenAll(activeSummaryTasks);
                }

                _folderFetchedOnce[folderToFetch] = true;

                if (_currentFolder == folderToFetch)
                {
                    int unreadCount = _emails.Count(e => !e.IsRead);
                    _logger.Report($"[✓] {folderName} sync complete. Loaded {_emails.Count} total email(s) ({unreadCount} unread).");

                    if (!settings.IsAiDisabled && string.Equals(settings.AiBackend, "LlamaCpp", StringComparison.OrdinalIgnoreCase) && settings.AutoStartLlamaServer)
                    {
                        if (settings.InstantVramUnload)
                        {
                            _llamaManager.Stop(_logger);
                            StatusUpdated?.Invoke(Lang.T(StringKeys.StatusSyncComplete), Lang.T(StringKeys.StatusVramFree));
                        }
                        else
                        {
                            StatusUpdated?.Invoke(Lang.T(StringKeys.StatusSyncComplete), Lang.T(StringKeys.StatusModelLoaded));
                        }
                    }
                    else if (settings.IsBatterySaverActive)
                    {
                        StatusUpdated?.Invoke(Lang.T(StringKeys.StatusSyncComplete), Lang.T(StringKeys.StatusBatterySaverNoAi));
                    }
                    else if (settings.IsAiDisabled)
                    {
                        StatusUpdated?.Invoke(Lang.T(StringKeys.StatusSyncComplete), Lang.T(StringKeys.StatusAiDisabled));
                    }
                    else
                    {
                        string backendMetric = string.Equals(settings.AiBackend, "Ollama", StringComparison.OrdinalIgnoreCase) 
                            ? Lang.T(StringKeys.StatusOllamaActive) 
                            : Lang.T(StringKeys.StatusCloudActive);
                        StatusUpdated?.Invoke(Lang.T(StringKeys.StatusSyncComplete), backendMetric);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Report($"[!] {folderName} sync cancelled.");
            }
            catch (Exception ex)
            {
                _logger.Report($"[!] {folderName} sync error: {ex.Message}");
            }
            finally
            {
                if (_currentFolder == folderToFetch)
                {
                    _isBatchSyncing = false;
                    _btnRefresh.Enabled = true;
                    _progressBar.Visible = false;
                }
            }
        }

        private async Task SummarizeUnreadEmailInBackgroundAsync(EmailItem email, AppSettings settings, Task<bool>? serverTask, CancellationToken ct)
        {
            try
            {
                email.Status = SummaryState.Pending;

                if (serverTask != null)
                {
                    await serverTask;
                }

                if (ct.IsCancellationRequested || this.IsDisposed)
                {
                    email.Status = SummaryState.Pending;
                    return;
                }

                // Acquire sequential queue lock (1-at-a-time inference to prevent KV cache collisions and cloud rate limits)
                await _summaryQueueSemaphore.WaitAsync(ct);
                try
                {
                    if (ct.IsCancellationRequested || this.IsDisposed)
                    {
                        email.Status = SummaryState.Pending;
                        return;
                    }

                    email.Status = SummaryState.Summarizing;
                    if (this.IsHandleCreated && !this.IsDisposed)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            if (!this.IsDisposed && this.IsHandleCreated)
                            {
                                UpdateListViewItemForEmail(email);
                            }
                        }));
                    }

                    string summary = await _llmService.SummarizeEmailAsync(email, settings, ct);
                    email.Summary = summary;
                    email.Status = SummaryState.Completed;

                    _logger.Report($"[✓] Background summary generated for: \"{email.Subject}\" (Priority {email.Priority ?? 2})");

                    if (this.IsDisposed || !this.IsHandleCreated) return;

                    this.BeginInvoke(new Action(() =>
                    {
                        if (this.IsDisposed || !this.IsHandleCreated) return;

                        UpdateListViewItemForEmail(email);

                        if (_lvEmails.SelectedItems.Count > 0 && GetCurrentPreviewEmail() == email)
                        {
                            DisplayEmail(email);
                        }
                    }));
                }
                finally
                {
                    _summaryQueueSemaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                email.Status = SummaryState.Pending;
            }
            catch
            {
                email.Status = SummaryState.Failed;
            }
        }

        private ListViewItem CreateListViewItemForEmail(EmailItem email, ListViewGroup group)
        {
            string subjectPrefix = email.IsArchived ? "📥 " : (email.IsRead ? "" : "● ");
            if (email.HasAttachments) subjectPrefix += "📎 ";

            bool isAiDisabled = _configService.Settings.IsAiDisabled;

            if (isAiDisabled)
            {
                var item = new ListViewItem(subjectPrefix + email.Subject, group)
                {
                    UseItemStyleForSubItems = false,
                    Tag = email
                };

                var subAccount = item.SubItems.Add(email.AccountName);
                var subSender = item.SubItems.Add(email.Sender);
                var subDate = item.SubItems.Add(email.DateString);

                if (email.IsArchived)
                {
                    var archivedColor = Color.FromArgb(150, 155, 165);
                    var regFont = new Font("Segoe UI", 8.75F, FontStyle.Regular);
                    item.ForeColor = archivedColor; item.Font = regFont;
                    subAccount.ForeColor = archivedColor; subAccount.Font = regFont;
                    subSender.ForeColor = archivedColor; subSender.Font = regFont;
                    subDate.ForeColor = archivedColor; subDate.Font = regFont;
                }
                else if (email.IsRead)
                {
                    var readColor = Color.FromArgb(130, 135, 145);
                    var regFont = new Font("Segoe UI", 8.75F, FontStyle.Regular);
                    item.ForeColor = readColor; item.Font = regFont;
                    subAccount.ForeColor = readColor; subAccount.Font = regFont;
                    subSender.ForeColor = readColor; subSender.Font = regFont;
                    subDate.ForeColor = readColor; subDate.Font = regFont;
                }
                else
                {
                    var unreadColor = Color.FromArgb(15, 15, 15);
                    var boldFont = new Font("Segoe UI", 9F, FontStyle.Bold);
                    var regFont = new Font("Segoe UI", 8.75F, FontStyle.Regular);
                    item.ForeColor = unreadColor; item.Font = boldFont;
                    subAccount.ForeColor = Color.FromArgb(70, 75, 85); subAccount.Font = regFont;
                    subSender.ForeColor = Color.FromArgb(60, 65, 75); subSender.Font = regFont;
                    subDate.ForeColor = Color.FromArgb(90, 95, 105); subDate.Font = regFont;
                }

                return item;
            }

            string priText;
            Color priColor;
            Font priFont;

            if (email.Priority.HasValue)
            {
                switch (email.Priority.Value)
                {
                    case 1:
                        priText = " 1 ";
                        priColor = Color.FromArgb(215, 30, 30); // Bold Crimson
                        priFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                        break;
                    case 2:
                        priText = " 2 ";
                        priColor = Color.FromArgb(0, 102, 204); // Bold Blue
                        priFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                        break;
                    case 3:
                    default:
                        priText = " 3 ";
                        priColor = Color.FromArgb(115, 120, 130); // Muted Slate
                        priFont = new Font("Segoe UI", 9F, FontStyle.Bold);
                        break;
                }
            }
            else
            {
                priText = email.Status == SummaryState.Summarizing ? "⏳" : "-";
                priColor = Color.FromArgb(160, 165, 175);
                priFont = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            }

            var itemNormal = new ListViewItem(priText, group)
            {
                UseItemStyleForSubItems = false,
                Tag = email,
                ForeColor = priColor,
                Font = priFont
            };

            var subSubject = itemNormal.SubItems.Add(subjectPrefix + email.Subject);
            var subAccNormal = itemNormal.SubItems.Add(email.AccountName);
            var subSendNormal = itemNormal.SubItems.Add(email.Sender);
            var subDateNormal = itemNormal.SubItems.Add(email.DateString);

            if (email.IsArchived)
            {
                var archivedColor = Color.FromArgb(150, 155, 165);
                var regFont = new Font("Segoe UI", 8.75F, FontStyle.Regular);
                subSubject.ForeColor = archivedColor; subSubject.Font = regFont;
                subAccNormal.ForeColor = archivedColor; subAccNormal.Font = regFont;
                subSendNormal.ForeColor = archivedColor; subSendNormal.Font = regFont;
                subDateNormal.ForeColor = archivedColor; subDateNormal.Font = regFont;
            }
            else if (email.IsRead)
            {
                var readColor = Color.FromArgb(130, 135, 145);
                var regFont = new Font("Segoe UI", 8.75F, FontStyle.Regular);
                subSubject.ForeColor = readColor; subSubject.Font = regFont;
                subAccNormal.ForeColor = readColor; subAccNormal.Font = regFont;
                subSendNormal.ForeColor = readColor; subSendNormal.Font = regFont;
                subDateNormal.ForeColor = readColor; subDateNormal.Font = regFont;
            }
            else
            {
                var unreadColor = Color.FromArgb(15, 15, 15);
                var boldFont = new Font("Segoe UI", 9F, FontStyle.Bold);
                var regFont = new Font("Segoe UI", 8.75F, FontStyle.Regular);
                subSubject.ForeColor = unreadColor; subSubject.Font = boldFont;
                subAccNormal.ForeColor = Color.FromArgb(70, 75, 85); subAccNormal.Font = regFont;
                subSendNormal.ForeColor = Color.FromArgb(60, 65, 75); subSendNormal.Font = regFont;
                subDateNormal.ForeColor = Color.FromArgb(90, 95, 105); subDateNormal.Font = regFont;
            }

            return itemNormal;
        }

        private void AddEmailItemToListView(EmailItem email)
        {
            if (email.Folder != _currentFolder) return;

            var filterAccount = _cboAccountFilter.SelectedItem?.ToString()?.Trim();
            string search = _txtSearch.Text.Trim();

            if (!string.IsNullOrEmpty(filterAccount) && 
                _cboAccountFilter.SelectedIndex > 0 && 
                !string.Equals(email.AccountName?.Trim(), filterAccount, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.IsNullOrEmpty(search))
            {
                bool matches = (email.Subject?.Contains(search, StringComparison.OrdinalIgnoreCase) == true) ||
                               (email.Sender?.Contains(search, StringComparison.OrdinalIgnoreCase) == true) ||
                               (email.AccountName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
                if (!matches) return;
            }

            string accName = string.IsNullOrWhiteSpace(email.AccountName) ? "Default Account" : email.AccountName.Trim();
            var group = _lvEmails.Groups[accName];
            if (group == null)
            {
                group = new ListViewGroup(accName, $"📬  {accName}");
                _lvEmails.Groups.Add(group);
            }

            var item = CreateListViewItemForEmail(email, group);
            _lvEmails.Items.Add(item);

            int unreadCount = _emails.Count(e => !e.IsRead);
            UpdateHeaderTitle(_emails.Count, unreadCount);
        }

        private void UpdateHeaderTitle(int totalCount, int unreadCount)
        {
            string folderName = _currentFolder switch
            {
                MailFolderType.Inbox => Lang.T(StringKeys.NavInbox),
                MailFolderType.Sent => Lang.T(StringKeys.NavSent),
                MailFolderType.Archive => Lang.T(StringKeys.NavArchived),
                MailFolderType.Spam => Lang.T(StringKeys.NavSpam),
                MailFolderType.Trash => Lang.T(StringKeys.NavTrash),
                _ => _currentFolder.ToString()
            };

            if (_currentFolder == MailFolderType.Inbox)
            {
                _lblInboxHeader.Text = Lang.Format(StringKeys.InboxListHeaderUnread, folderName, totalCount, unreadCount);
            }
            else
            {
                _lblInboxHeader.Text = Lang.Format(StringKeys.InboxListHeader, folderName, totalCount);
            }
        }

        private void ResetEmailPreview()
        {
            _selectedEmailsOrder.Clear();
            _txtSummary.Clear();
            _rtbEmailBody.Clear();
            ResetLinkToolTip();
            _currentEmailLinkSpans.Clear();
            _lblEmailSubject.Text = $"{Lang.T(StringKeys.InboxSubjectPrefix)} {Lang.T(StringKeys.InboxNoEmailSelected)}";
            _lblEmailMeta.Text = $"{Lang.T(StringKeys.InboxDetailFrom)} -   •   {Lang.T(StringKeys.InboxDetailDate)} -   •   {Lang.T(StringKeys.InboxDetailAccount)} -";
            _btnReply.Visible = false;
            _btnMoveToInbox.Visible = false;
            if (_sliderSubject != null)
            {
                _sliderSubject.Visible = false;
                _sliderSubject.Value = 0;
                _lblEmailSubject.Location = new Point(0, 0);
            }
            _pnlAttachments.Visible = false;
            _pnlAttachments.Controls.Clear();
            UpdateSummaryPaneVisibility(null);
        }

        private void PopulateListView()
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            if (this.InvokeRequired)
            {
                try
                {
                    this.BeginInvoke(new Action(PopulateListView));
                }
                catch { }
                return;
            }

            ResetInboxToolTip();
            _lvEmails.BeginUpdate();
            _lvEmails.Items.Clear();
            _lvEmails.Groups.Clear();

            var filterAccount = _cboAccountFilter.SelectedItem?.ToString()?.Trim();
            string search = _txtSearch.Text.Trim();

            var visibleEmails = _emails.Where(e =>
            {
                if (e.Folder != _currentFolder) return false;

                if (!string.IsNullOrEmpty(filterAccount) && 
                    _cboAccountFilter.SelectedIndex > 0 && 
                    !string.Equals(e.AccountName?.Trim(), filterAccount, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(search))
                {
                    return (e.Subject?.Contains(search, StringComparison.OrdinalIgnoreCase) == true) ||
                           (e.Sender?.Contains(search, StringComparison.OrdinalIgnoreCase) == true) ||
                           (e.AccountName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
                }

                return true;
            }).ToList();

            int unreadCount = visibleEmails.Count(e => !e.IsRead);
            UpdateHeaderTitle(visibleEmails.Count, unreadCount);

            var groupsDict = new Dictionary<string, ListViewGroup>(StringComparer.OrdinalIgnoreCase);

            foreach (var email in visibleEmails)
            {
                string accName = string.IsNullOrWhiteSpace(email.AccountName) ? "Default Account" : email.AccountName.Trim();
                if (!groupsDict.TryGetValue(accName, out var group))
                {
                    group = new ListViewGroup(accName, $"📬  {accName}");
                    groupsDict[accName] = group;
                    _lvEmails.Groups.Add(group);
                }

                var item = CreateListViewItemForEmail(email, group);
                _lvEmails.Items.Add(item);
            }

            _lvEmails.EndUpdate();

            if (_lvEmails.Items.Count > 0)
            {
                _lvEmails.Items[0].Selected = true;
            }
            else
            {
                _txtSummary.Clear();
                _rtbEmailBody.Clear();
                ResetLinkToolTip();
                _currentEmailLinkSpans.Clear();
                _lblEmailSubject.Text = "Subject: (No email selected)";
                _lblEmailMeta.Text = "From: -   •   Date: -   •   Account: -";
                _btnReply.Visible = false;
                _btnMoveToInbox.Visible = false;
            }
        }

        private void ApplyFilter()
        {
            PopulateListView();
        }

        private void OnEmailItemSelectionChanged(object? sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (e?.Item?.Tag is EmailItem email)
            {
                if (e.IsSelected)
                {
                    _selectedEmailsOrder.Remove(email);
                    _selectedEmailsOrder.Add(email);
                }
                else
                {
                    _selectedEmailsOrder.Remove(email);
                }
            }
        }

        private async void OnEmailSelected(object? sender, EventArgs e)
        {
            if (_lvEmails.SelectedItems.Count == 0)
            {
                ResetEmailPreview();
                return;
            }

            var currentSelectedTags = _lvEmails.SelectedItems.Cast<ListViewItem>()
                .Select(i => i.Tag as EmailItem)
                .Where(x => x != null)
                .ToHashSet();
            _selectedEmailsOrder.RemoveAll(x => !currentSelectedTags.Contains(x));

            var email = GetCurrentPreviewEmail();
            if (email == null) return;

            DisplayEmail(email);

            // If this email is already summarizing in the background, do not start a duplicate task
            if (email.Status == SummaryState.Summarizing)
            {
                return;
            }

            // If batch sync is actively running and this email is unread (queued for auto-summarization), let the batch task handle it
            if (_isBatchSyncing && !email.IsRead && string.IsNullOrWhiteSpace(email.Summary))
            {
                return;
            }

            // If AI is disabled, do not run summarization or start LLM servers
            if (_configService.Settings.IsAiDisabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(email.Summary) || 
                email.Summary.Contains("Loading model", StringComparison.OrdinalIgnoreCase) ||
                email.Summary.StartsWith("(LLM Error", StringComparison.OrdinalIgnoreCase) ||
                email.Summary.StartsWith("(Could not reach LLM", StringComparison.OrdinalIgnoreCase))
            {
                _txtSummary.Text = "✨ Generating AI summary for this email...";
                StatusUpdated?.Invoke($"Summarizing \"{email.Subject}\"...", "In Use (GPU)");

                email.Status = SummaryState.Summarizing;
                UpdateListViewItemForEmail(email);

                var settings = _configService.Settings;
                if (string.Equals(settings.AiBackend, "LlamaCpp", StringComparison.OrdinalIgnoreCase) && settings.AutoStartLlamaServer)
                {
                    await _llamaManager.StartAsync(
                        settings.LlamaModelPath,
                        settings.LlamaServerPort,
                        settings.LlamaGpuLayers,
                        contextSize: settings.LlamaContextSize,
                        logger: _logger);
                }

                string summary = await _llmService.SummarizeEmailAsync(email, settings);
                email.Summary = summary;
                email.Status = SummaryState.Completed;

                UpdateListViewItemForEmail(email);

                if (_lvEmails.SelectedItems.Count > 0 && GetCurrentPreviewEmail() == email)
                {
                    DisplayEmail(email);
                }

                // If InstantVramUnload is requested and we are using local llama.cpp, free VRAM now
                if (string.Equals(settings.AiBackend, "LlamaCpp", StringComparison.OrdinalIgnoreCase) && settings.AutoStartLlamaServer)
                {
                    if (settings.InstantVramUnload && !_isBatchSyncing)
                    {
                        _llamaManager.Stop(_logger);
                        StatusUpdated?.Invoke("Summary ready", "VRAM Free");
                    }
                    else
                    {
                        StatusUpdated?.Invoke("Summary ready", "Model Loaded in VRAM");
                    }
                }
                else
                {
                    string backendMetric = string.Equals(settings.AiBackend, "Ollama", StringComparison.OrdinalIgnoreCase) 
                        ? "Ollama Active" 
                        : "Cloud Active";
                    StatusUpdated?.Invoke("Summary ready", backendMetric);
                }
            }
        }

        private EmailItem? GetCurrentPreviewEmail()
        {
            if (_lvEmails.SelectedItems.Count == 0) return null;

            string previewMode = _configService.Settings.MultiSelectPreview;
            if (string.Equals(previewMode, "FirstSelected", StringComparison.OrdinalIgnoreCase))
            {
                return _selectedEmailsOrder.FirstOrDefault() ?? (_lvEmails.SelectedItems[0].Tag as EmailItem);
            }
            
            return _selectedEmailsOrder.LastOrDefault() ?? (_lvEmails.SelectedItems[_lvEmails.SelectedItems.Count - 1].Tag as EmailItem);
        }

        private void UpdateActionButtonsVisibility()
        {
            var previewEmail = GetCurrentPreviewEmail();
            if (previewEmail == null)
            {
                _btnReply.Visible = false;
                _btnMoveToInbox.Visible = false;
                return;
            }

            float scale = this.DeviceDpi / 96f;
            if (_currentFolder == MailFolderType.Spam)
            {
                _btnReply.Visible = false;
                _btnMoveToInbox.Visible = true;
                _pnlReplyBox.Width = (int)(130 * scale);
            }
            else
            {
                _btnMoveToInbox.Visible = false;
                _btnReply.Visible = true;
                _pnlReplyBox.Width = (int)(88 * scale);
            }
            UpdateSubjectSlider();
        }

        private void DisplayEmail(EmailItem email)
        {
            UpdateSummaryPaneVisibility(email);

            string readTag = email.IsRead ? $"[{Lang.T(StringKeys.InboxTagRead)}]" : $"[{Lang.T(StringKeys.InboxTagUnread)}]";
            if (email.IsArchived) readTag = $"[{Lang.T(StringKeys.InboxTagArchived)}] • " + readTag;

            string priTag = email.Priority.HasValue
                ? $"   •   {Lang.T(StringKeys.InboxColPriority)}: {email.Priority.Value} ({(email.Priority.Value == 1 ? Lang.T(StringKeys.InboxPriorityHigh) : email.Priority.Value == 2 ? Lang.T(StringKeys.InboxPriorityNormal) : Lang.T(StringKeys.InboxPriorityLow))})"
                : "";

            _lblEmailSubject.Text = $"{Lang.T(StringKeys.InboxSubjectPrefix)} {email.Subject}";
            UpdateSubjectSlider();
            _lblEmailMeta.Text = $"{Lang.T(StringKeys.InboxDetailFrom)} {email.Sender}   •   {Lang.T(StringKeys.InboxDetailDate)} {email.DateString}   •   {Lang.T(StringKeys.InboxDetailAccount)} {email.AccountName}{priTag}   •   {readTag}";
            UpdateActionButtonsVisibility();

            try
            {
                _inboxCellToolTip?.SetToolTip(_lblEmailSubject, email.Subject);
                _inboxCellToolTip?.SetToolTip(_lblEmailMeta, $"From: {email.Sender}\r\nDate: {GetDateToolTipText(email)}\r\nAccount: {GetAccountToolTipText(email)}");
            }
            catch { }

            bool hasValidSummary = !string.IsNullOrWhiteSpace(email.Summary) && 
                                   !email.Summary.StartsWith("✨", StringComparison.OrdinalIgnoreCase) &&
                                   !email.Summary.StartsWith("(LLM Error", StringComparison.OrdinalIgnoreCase) &&
                                   !email.Summary.StartsWith("(Could not reach", StringComparison.OrdinalIgnoreCase);

            if (!_configService.Settings.IsAiDisabled || hasValidSummary)
            {
                string summaryText = string.IsNullOrWhiteSpace(email.Summary) 
                    ? "✨ Generating AI summary for this email..." 
                    : email.Summary;

                if (email.IsArchived && !summaryText.StartsWith("📥 ", StringComparison.OrdinalIgnoreCase) && !summaryText.StartsWith("[Archived] ", StringComparison.OrdinalIgnoreCase))
                {
                    summaryText = "📥 " + summaryText;
                }

                _txtSummary.Text = summaryText;
            }

            if (!string.IsNullOrWhiteSpace(email.DisplayRtf))
            {
                try
                {
                    _rtbEmailBody.Rtf = email.DisplayRtf;
                }
                catch
                {
                    _rtbEmailBody.Text = !string.IsNullOrWhiteSpace(email.DisplayBody) ? email.DisplayBody : email.CleanBody;
                }
            }
            else
            {
                _rtbEmailBody.Text = !string.IsNullOrWhiteSpace(email.DisplayBody) ? email.DisplayBody : email.CleanBody;
            }

            UpdateEmailLinkSpans(email);
            UpdateEmailAttachments(email);
        }

        private void UpdateSubjectSlider()
        {
            if (this.IsDisposed || !this.IsHandleCreated || _pnlSubjectViewport == null || _sliderSubject == null || _lblEmailSubject == null) return;

            float scale = this.DeviceDpi / 96f;
            int textWidth = TextRenderer.MeasureText(_lblEmailSubject.Text, _lblEmailSubject.Font).Width;
            int visibleWidth = _pnlSubjectViewport.ClientSize.Width;

            if (textWidth > visibleWidth && visibleWidth > (int)(40 * scale))
            {
                int maxScroll = textWidth - visibleWidth;
                int largeChange = Math.Max(10, visibleWidth / 4);
                _sliderSubject.Minimum = 0;
                _sliderSubject.LargeChange = largeChange;
                _sliderSubject.SmallChange = Math.Max(5, visibleWidth / 10);
                _sliderSubject.Maximum = maxScroll + largeChange - 1;

                if (!_sliderSubject.Visible)
                {
                    _sliderSubject.Visible = true;
                    _sliderSubject.Value = 0;
                    _lblEmailSubject.Location = new Point(0, (int)(2 * scale));
                }
                else
                {
                    int clampedVal = Math.Clamp(_sliderSubject.Value, 0, maxScroll);
                    _sliderSubject.Value = clampedVal;
                    _lblEmailSubject.Location = new Point(-clampedVal, (int)(2 * scale));
                }
            }
            else
            {
                if (_sliderSubject.Visible)
                {
                    _sliderSubject.Visible = false;
                    _sliderSubject.Value = 0;
                }
                _lblEmailSubject.Location = new Point(0, (int)(2 * scale));
            }
        }

        private void OnEmailLinkClicked(object? sender, LinkClickedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.LinkText)) return;

            try
            {
                string url = e.LinkText.Trim();

                // If LinkText is an anchor label (like "[Remote Content]" or "Click Here"), resolve target URL from link spans
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                {
                    Point mousePos = _rtbEmailBody.PointToClient(Cursor.Position);
                    int charIdx = _rtbEmailBody.GetCharIndexFromPosition(mousePos);
                    var span = _currentEmailLinkSpans.FirstOrDefault(s => charIdx >= s.Start - 2 && charIdx <= s.Start + s.Length + 2);
                    if (!string.IsNullOrEmpty(span.Url))
                    {
                        url = span.Url;
                    }
                    else if (!string.IsNullOrEmpty(_lastHoverLinkUrl))
                    {
                        url = _lastHoverLinkUrl;
                    }
                }

                // Check and launch valid http, https, mailto URLs
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = uri.AbsoluteUri,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                else if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase) &&
                         Uri.TryCreate("https://" + url, UriKind.Absolute, out var wwwUri) &&
                         wwwUri.Scheme == Uri.UriSchemeHttps)
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = wwwUri.AbsoluteUri,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                _logger?.Report($"[!] Failed to open link: {ex.Message}");
            }
        }

        private void UpdateListViewItemForEmail(EmailItem email)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            bool isAiDisabled = _configService.Settings.IsAiDisabled;

            foreach (ListViewItem item in _lvEmails.Items)
            {
                if (item.Tag == email)
                {
                    string subjectPrefix = email.IsArchived ? "📥 " : (email.IsRead ? "" : "● ");
                    if (email.HasAttachments) subjectPrefix += "📎 ";

                    if (isAiDisabled)
                    {
                        item.Text = subjectPrefix + email.Subject;
                        if (email.IsArchived)
                        {
                            item.ForeColor = Color.FromArgb(150, 155, 165);
                            item.Font = new Font("Segoe UI", 8.75F, FontStyle.Regular);
                        }
                        else if (email.IsRead)
                        {
                            item.ForeColor = Color.FromArgb(130, 135, 145);
                            item.Font = new Font("Segoe UI", 8.75F, FontStyle.Regular);
                        }
                        else
                        {
                            item.ForeColor = Color.FromArgb(15, 15, 15);
                            item.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                        }

                        if (item.SubItems.Count > 1) item.SubItems[1].Text = email.AccountName;
                        if (item.SubItems.Count > 2) item.SubItems[2].Text = email.Sender;
                        if (item.SubItems.Count > 3) item.SubItems[3].Text = email.DateString;
                    }
                    else
                    {
                        if (email.Priority.HasValue)
                        {
                            switch (email.Priority.Value)
                            {
                                case 1:
                                    item.Text = " 1 ";
                                    item.ForeColor = Color.FromArgb(215, 30, 30);
                                    item.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                                    break;
                                case 2:
                                    item.Text = " 2 ";
                                    item.ForeColor = Color.FromArgb(0, 102, 204);
                                    item.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                                    break;
                                case 3:
                                default:
                                    item.Text = " 3 ";
                                    item.ForeColor = Color.FromArgb(115, 120, 130);
                                    item.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                                    break;
                            }
                        }
                        else
                        {
                            item.Text = email.Status == SummaryState.Summarizing ? "⏳" : "-";
                            item.ForeColor = Color.FromArgb(160, 165, 175);
                            item.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
                        }

                        if (item.SubItems.Count > 1)
                        {
                            item.SubItems[1].Text = subjectPrefix + email.Subject;

                            if (email.IsArchived)
                            {
                                item.SubItems[1].ForeColor = Color.FromArgb(150, 155, 165);
                                item.SubItems[1].Font = new Font("Segoe UI", 8.75F, FontStyle.Regular);
                            }
                            else if (email.IsRead)
                            {
                                item.SubItems[1].ForeColor = Color.FromArgb(130, 135, 145);
                                item.SubItems[1].Font = new Font("Segoe UI", 8.75F, FontStyle.Regular);
                            }
                            else
                            {
                                item.SubItems[1].ForeColor = Color.FromArgb(15, 15, 15);
                                item.SubItems[1].Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                            }
                        }
                    }
                    break;
                }
            }
        }

        private List<EmailItem> GetSelectedEmailItems()
        {
            var list = new List<EmailItem>();
            foreach (ListViewItem item in _lvEmails.SelectedItems)
            {
                if (item.Tag is EmailItem email)
                {
                    list.Add(email);
                }
            }
            return list;
        }

        private void OnArchiveClick(object? sender, EventArgs e)
        {
            var selected = GetSelectedEmailItems();
            if (selected.Count == 0) return;

            foreach (var email in selected)
            {
                email.IsArchived = true;
                UpdateListViewItemForEmail(email);

                lock (_folderStorage)
                {
                    if (_folderStorage.TryGetValue(MailFolderType.Archive, out var archList))
                    {
                        bool exists = email.UniqueId > 0
                            ? archList.Any(e => e.UniqueId == email.UniqueId && string.Equals(e.AccountName, email.AccountName, StringComparison.OrdinalIgnoreCase))
                            : archList.Contains(email);

                        if (!exists)
                        {
                            var archCopy = email.Clone();
                            archCopy.Folder = MailFolderType.Archive;
                            archList.Add(archCopy);
                        }
                    }
                }
            }

            var previewEmail = GetCurrentPreviewEmail();
            if (previewEmail != null)
            {
                DisplayEmail(previewEmail);
            }

            var sourceFolder = _currentFolder;
            if (selected.Count > 1)
            {
                var task = ExecuteImapTriageAsync(selected.Select(em => new PendingTriageItem { Email = em, Action = TriageActionType.Archive, SourceFolder = sourceFolder }).ToList());
                TrackInFlightTask(task);
            }
            else
            {
                QueueSingleTriage(selected[0], TriageActionType.Archive, sourceFolder);
            }
        }

        private void OnDeleteClick(object? sender, EventArgs e)
        {
            var selected = GetSelectedEmailItems();
            if (selected.Count == 0) return;

            ResetInboxToolTip();
            _lvEmails.BeginUpdate();
            foreach (var email in selected)
            {
                _emails.Remove(email);
                _selectedEmailsOrder.Remove(email);

                lock (_folderStorage)
                {
                    _folderStorage[_currentFolder].Remove(email);
                    if (_currentFolder != MailFolderType.Trash && _folderStorage.TryGetValue(MailFolderType.Trash, out var trashList))
                    {
                        bool exists = email.UniqueId > 0
                            ? trashList.Any(e => e.UniqueId == email.UniqueId && string.Equals(e.AccountName, email.AccountName, StringComparison.OrdinalIgnoreCase))
                            : trashList.Contains(email);

                        if (!exists)
                        {
                            var trashCopy = email.Clone();
                            trashCopy.Folder = MailFolderType.Trash;
                            trashList.Add(trashCopy);
                        }
                    }
                }

                ListViewItem? foundItem = null;
                foreach (ListViewItem lvi in _lvEmails.Items)
                {
                    if (lvi.Tag == email)
                    {
                        foundItem = lvi;
                        break;
                    }
                }
                if (foundItem != null)
                {
                    _lvEmails.Items.Remove(foundItem);
                }
            }
            _lvEmails.EndUpdate();

            int unreadCount = _emails.Count(em => !em.IsRead);
            UpdateHeaderTitle(_emails.Count, unreadCount);

            if (_lvEmails.Items.Count > 0)
            {
                if (_lvEmails.SelectedItems.Count == 0)
                {
                    _lvEmails.Items[0].Selected = true;
                }
                else
                {
                    var previewEmail = GetCurrentPreviewEmail();
                    if (previewEmail != null)
                    {
                        DisplayEmail(previewEmail);
                    }
                }
            }
            else
            {
                ResetEmailPreview();
            }

            var sourceFolder = _currentFolder;
            if (selected.Count > 1)
            {
                var task = ExecuteImapTriageAsync(selected.Select(em => new PendingTriageItem { Email = em, Action = TriageActionType.Delete, SourceFolder = sourceFolder }).ToList());
                TrackInFlightTask(task);
            }
            else
            {
                QueueSingleTriage(selected[0], TriageActionType.Delete, sourceFolder);
            }
        }

        private void OnMoveToInboxClick(object? sender, EventArgs e)
        {
            var selected = GetSelectedEmailItems();
            if (selected.Count == 0)
            {
                var current = GetCurrentPreviewEmail();
                if (current != null) selected.Add(current);
            }
            if (selected.Count == 0) return;

            ResetInboxToolTip();
            _lvEmails.BeginUpdate();
            foreach (var email in selected)
            {
                _emails.Remove(email);
                _selectedEmailsOrder.Remove(email);

                lock (_folderStorage)
                {
                    _folderStorage[_currentFolder].Remove(email);
                    if (_folderStorage.TryGetValue(MailFolderType.Inbox, out var inboxList))
                    {
                        bool exists = email.UniqueId > 0
                            ? inboxList.Any(i => i.UniqueId == email.UniqueId && string.Equals(i.AccountName, email.AccountName, StringComparison.OrdinalIgnoreCase))
                            : inboxList.Contains(email);

                        if (!exists)
                        {
                            var inboxCopy = email.Clone();
                            inboxCopy.Folder = MailFolderType.Inbox;
                            inboxList.Insert(0, inboxCopy);
                        }
                    }
                }

                ListViewItem? foundItem = null;
                foreach (ListViewItem lvi in _lvEmails.Items)
                {
                    if (lvi.Tag == email)
                    {
                        foundItem = lvi;
                        break;
                    }
                }
                if (foundItem != null)
                {
                    _lvEmails.Items.Remove(foundItem);
                }
            }
            _lvEmails.EndUpdate();

            int unreadCount = _emails.Count(em => !em.IsRead);
            UpdateHeaderTitle(_emails.Count, unreadCount);

            if (_lvEmails.Items.Count > 0)
            {
                if (_lvEmails.SelectedItems.Count == 0)
                {
                    _lvEmails.Items[0].Selected = true;
                }
                else
                {
                    var previewEmail = GetCurrentPreviewEmail();
                    if (previewEmail != null)
                    {
                        DisplayEmail(previewEmail);
                    }
                }
            }
            else
            {
                ResetEmailPreview();
            }

            var sourceFolder = _currentFolder;
            if (selected.Count > 1)
            {
                var task = ExecuteImapTriageAsync(selected.Select(em => new PendingTriageItem { Email = em, Action = TriageActionType.MoveToInbox, SourceFolder = sourceFolder }).ToList());
                TrackInFlightTask(task);
            }
            else
            {
                QueueSingleTriage(selected[0], TriageActionType.MoveToInbox, sourceFolder);
            }
        }

        private void QueueSingleTriage(EmailItem email, TriageActionType action, MailFolderType sourceFolder)
        {
            lock (_triageLock)
            {
                _pendingSingleTriage.RemoveAll(x => x.Email.UniqueId == email.UniqueId && string.Equals(x.Email.AccountEmail, email.AccountEmail, StringComparison.OrdinalIgnoreCase));
                _pendingSingleTriage.Add(new PendingTriageItem { Email = email, Action = action, SourceFolder = sourceFolder });

                _debounceTriageTimer?.Dispose();
                _debounceTriageTimer = new System.Threading.Timer(_ =>
                {
                    FlushPendingSingleTriage();
                }, null, 1500, Timeout.Infinite);
            }
        }

        private void FlushPendingSingleTriage()
        {
            List<PendingTriageItem> itemsToFlush;
            lock (_triageLock)
            {
                _debounceTriageTimer?.Dispose();
                _debounceTriageTimer = null;

                if (_pendingSingleTriage.Count == 0) return;
                itemsToFlush = new List<PendingTriageItem>(_pendingSingleTriage);
                _pendingSingleTriage.Clear();
            }

            var task = ExecuteImapTriageAsync(itemsToFlush);
            TrackInFlightTask(task);
        }

        private async Task ExecuteImapTriageAsync(List<PendingTriageItem> items)
        {
            if (items.Count == 0) return;

            var accounts = _configService.GetAccounts();
            var deleteItems = items.Where(i => i.Action == TriageActionType.Delete).ToList();
            var archiveItems = items.Where(i => i.Action == TriageActionType.Archive).ToList();
            var moveToInboxItems = items.Where(i => i.Action == TriageActionType.MoveToInbox).ToList();

            var tasks = new List<Task>();

            if (deleteItems.Count > 0)
            {
                foreach (var item in deleteItems)
                {
                    item.Email.Folder = item.SourceFolder;
                }
                tasks.Add(_imapService.DeleteEmailsBatchAsync(deleteItems.Select(i => i.Email), accounts, _logger));
            }

            if (archiveItems.Count > 0)
            {
                foreach (var item in archiveItems)
                {
                    item.Email.Folder = item.SourceFolder;
                }
                tasks.Add(_imapService.ArchiveEmailsBatchAsync(archiveItems.Select(i => i.Email), accounts, _logger));
            }

            if (moveToInboxItems.Count > 0)
            {
                foreach (var item in moveToInboxItems)
                {
                    item.Email.Folder = item.SourceFolder;
                }
                tasks.Add(_imapService.MoveToInboxEmailsBatchAsync(moveToInboxItems.Select(i => i.Email), accounts, _logger));
            }

            await Task.WhenAll(tasks);
        }

        private void TrackInFlightTask(Task task)
        {
            lock (_inFlightTasks)
            {
                _inFlightTasks.Add(task);
            }
            _ = task.ContinueWith(t =>
            {
                lock (_inFlightTasks)
                {
                    _inFlightTasks.Remove(task);
                }
            });
        }

        public bool HasPendingOrInFlightTriage
        {
            get
            {
                lock (_triageLock)
                {
                    if (_pendingSingleTriage.Count > 0) return true;
                }
                lock (_inFlightTasks)
                {
                    return _inFlightTasks.Any(t => !t.IsCompleted);
                }
            }
        }

        public async Task FlushPendingTriageAsync()
        {
            List<PendingTriageItem> itemsToFlush;
            lock (_triageLock)
            {
                _debounceTriageTimer?.Dispose();
                _debounceTriageTimer = null;
                itemsToFlush = new List<PendingTriageItem>(_pendingSingleTriage);
                _pendingSingleTriage.Clear();
            }

            if (itemsToFlush.Count > 0)
            {
                var task = ExecuteImapTriageAsync(itemsToFlush);
                TrackInFlightTask(task);
            }

            List<Task> currentTasks;
            lock (_inFlightTasks)
            {
                currentTasks = _inFlightTasks.Where(t => !t.IsCompleted).ToList();
            }

            if (currentTasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(currentTasks).WaitAsync(TimeSpan.FromSeconds(8));
                }
                catch { }
            }
        }

        private void OnCopySummaryClick(object? sender, EventArgs e)
        {
            if (_lvEmails.SelectedItems.Count > 0 && _lvEmails.SelectedItems[0].Tag is EmailItem email && !string.IsNullOrWhiteSpace(email.Summary))
            {
                Clipboard.SetText($"[{email.AccountName}] {email.Subject}\r\nFrom: {email.Sender}\r\n\r\nSummary:\r\n{email.Summary}");
                MessageBox.Show(Lang.T(StringKeys.InboxSummaryCopiedToast), Lang.T(StringKeys.CommonSuccess), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (_emails.Any(x => !string.IsNullOrWhiteSpace(x.Summary)))
            {
                var sb = new StringBuilder();
                sb.AppendLine($"# Email Summaries ({DateTime.Now:g})");
                sb.AppendLine();
                foreach (var em in _emails.Where(x => !string.IsNullOrWhiteSpace(x.Summary)))
                {
                    sb.AppendLine($"### [{em.AccountName}] {em.Subject}");
                    sb.AppendLine($"**From:** {em.Sender} | **Date:** {em.DateString}");
                    sb.AppendLine();
                    sb.AppendLine(em.Summary);
                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                }
                Clipboard.SetText(sb.ToString());
                MessageBox.Show(Lang.T(StringKeys.InboxAllSummariesCopiedToast), Lang.T(StringKeys.CommonSuccess), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(Lang.T(StringKeys.InboxNoSummaryToCopy), Lang.T(StringKeys.CommonWarning), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnExportClick(object? sender, EventArgs e)
        {
            if (!_emails.Any())
            {
                MessageBox.Show(Lang.T(StringKeys.InboxNoEmailsToExport), Lang.T(StringKeys.CommonWarning), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = $"Email_Summaries_{DateTime.Now:yyyyMMdd_HHmmss}.md",
                Title = "Export Email Summaries"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"# AI Email Summaries");
                    sb.AppendLine($"*Generated on: {DateTime.Now:f}*");
                    sb.AppendLine();

                    foreach (var email in _emails)
                    {
                        sb.AppendLine($"## [{email.AccountName}] {email.Subject}");
                        sb.AppendLine($"- **From:** {email.Sender}");
                        sb.AppendLine($"- **Date:** {email.DateString}");
                        sb.AppendLine($"- **Status:** {(email.IsRead ? "Read" : "Unread")}");
                        sb.AppendLine();
                        sb.AppendLine("### AI Summary:");
                        sb.AppendLine(string.IsNullOrWhiteSpace(email.Summary) ? "*(No summary generated)*" : email.Summary);
                        sb.AppendLine();
                        sb.AppendLine("<details><summary>View Original Email Body</summary>");
                        sb.AppendLine();
                        sb.AppendLine("```text");
                        sb.AppendLine(!string.IsNullOrWhiteSpace(email.DisplayBody) ? email.DisplayBody : email.CleanBody);
                        sb.AppendLine("```");
                        sb.AppendLine("</details>");
                        sb.AppendLine();
                        sb.AppendLine("---");
                        sb.AppendLine();
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show(Lang.Format(StringKeys.InboxExportSuccessToast, Path.GetFileName(sfd.FileName)), Lang.T(StringKeys.InboxExportSuccessTitle), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Lang.Format(StringKeys.InboxExportErrorToast, ex.Message), Lang.T(StringKeys.InboxExportErrorTitle), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string? _topBarIconFontFamily;
        private static string GetTopBarIconFontFamily()
        {
            if (_topBarIconFontFamily != null) return _topBarIconFontFamily;
            try
            {
                using var installedFonts = new System.Drawing.Text.InstalledFontCollection();
                var set = new HashSet<string>(installedFonts.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
                if (set.Contains("Segoe Fluent Icons")) return _topBarIconFontFamily = "Segoe Fluent Icons";
                if (set.Contains("Segoe MDL2 Assets")) return _topBarIconFontFamily = "Segoe MDL2 Assets";
            }
            catch { }
            return _topBarIconFontFamily = "Segoe UI Emoji";
        }

        private async void OnOpenInBrowserClick(object? sender, EventArgs e)
        {
            var email = GetCurrentPreviewEmail();
            if (email == null)
            {
                _logger?.Report("[*] Select an email from the inbox list to open it in your browser.");
                return;
            }

            try
            {
                _logger?.Report($"[*] Opening \"{email.Subject}\" in default web browser...");

                string? htmlContent = email.HtmlBody;

                // If HtmlBody was not cached during initial fetch, try fetching on-demand from IMAP
                if (string.IsNullOrWhiteSpace(htmlContent) && email.UniqueId > 0 && !string.IsNullOrWhiteSpace(email.AccountName))
                {
                    var account = _configService.GetAccounts().FirstOrDefault(a => 
                        string.Equals(a.Name, email.AccountName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a.Email, email.AccountEmail, StringComparison.OrdinalIgnoreCase));

                    if (account != null)
                    {
                        try
                        {
                            htmlContent = await _imapService.FetchEmailHtmlBodyAsync(account, email.UniqueId, email.Folder);
                            if (!string.IsNullOrWhiteSpace(htmlContent))
                            {
                                email.HtmlBody = htmlContent;
                            }
                        }
                        catch { }
                    }
                }

                string renderedHtml = BuildBrowserHtmlPage(email, htmlContent);

                string tempDir = ConfigService.TempFolder;
                Directory.CreateDirectory(tempDir);

                // Clean file name
                string safeSubject = string.Concat(email.Subject.Split(Path.GetInvalidFileNameChars())).Trim();
                if (string.IsNullOrWhiteSpace(safeSubject)) safeSubject = "email";
                if (safeSubject.Length > 30) safeSubject = safeSubject.Substring(0, 30);

                string tempFile = Path.Combine(tempDir, $"{safeSubject}_{email.UniqueId}_{DateTime.UtcNow.Ticks}.html");
                await File.WriteAllTextAsync(tempFile, renderedHtml, Encoding.UTF8);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempFile,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);

                _logger?.Report("[✓] Email opened in web browser successfully.");
            }
            catch (Exception ex)
            {
                _logger?.Report($"[!] Failed to open email in browser: {ex.Message}");
                MessageBox.Show(Lang.Format(StringKeys.InboxBrowserErrorToast, ex.Message), Lang.T(StringKeys.InboxBrowserErrorTitle), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string BuildBrowserHtmlPage(EmailItem email, string? htmlBody)
        {
            string encSubject = WebUtility.HtmlEncode(email.Subject);
            string encSender = WebUtility.HtmlEncode(email.Sender);
            string encDate = WebUtility.HtmlEncode(email.DateString);
            string encAccount = WebUtility.HtmlEncode(email.AccountName);

            string headerBanner = $@"
<div style=""background: #f8f9fa; border-bottom: 2px solid #e2e8f0; padding: 16px 24px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #2d3748; margin: 0 0 20px 0; box-shadow: 0 1px 3px rgba(0,0,0,0.06);"">
    <div style=""font-size: 19px; font-weight: 700; color: #1a202c; margin-bottom: 6px;"">{encSubject}</div>
    <div style=""font-size: 13px; color: #4a5568; line-height: 1.5;"">
        <strong>From:</strong> {encSender} &bull; 
        <strong>Date:</strong> {encDate} &bull; 
        <strong>Account:</strong> {encAccount}
    </div>
</div>";

            if (!string.IsNullOrWhiteSpace(htmlBody))
            {
                // If it's already a full HTML document with <body>, inject the header banner right after <body>
                int bodyIdx = htmlBody.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
                if (bodyIdx >= 0)
                {
                    int bodyCloseIdx = htmlBody.IndexOf('>', bodyIdx);
                    if (bodyCloseIdx >= 0)
                    {
                        string before = htmlBody.Substring(0, bodyCloseIdx + 1);
                        string after = htmlBody.Substring(bodyCloseIdx + 1);
                        return before + headerBanner + after;
                    }
                }

                // If no <body> tag, wrap in a clean document structure
                return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>{encSubject}</title>
</head>
<body style=""margin: 0; padding: 0; background: #ffffff;"">
    {headerBanner}
    <div style=""padding: 0 24px 30px 24px;"">
        {htmlBody}
    </div>
</body>
</html>";
            }

            // Plaintext fallback
            string bodyText = !string.IsNullOrWhiteSpace(email.DisplayBody) ? email.DisplayBody : email.CleanBody;
            string encContent = WebUtility.HtmlEncode(bodyText);

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>{encSubject}</title>
    <style>
        body {{ margin: 0; padding: 0; background: #fdfdfd; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }}
        .content-body {{ padding: 10px 24px 40px 24px; font-size: 15px; line-height: 1.65; color: #2d3748; white-space: pre-wrap; word-wrap: break-word; }}
    </style>
</head>
<body>
    {headerBanner}
    <div class=""content-body"">{encContent}</div>
</body>
</html>";
        }

        public void CancelRunningOperations()
        {
            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CancelRunningOperations();
                lock (_triageLock)
                {
                    _debounceTriageTimer?.Dispose();
                    _debounceTriageTimer = null;
                }
                _inboxHoverTimer?.Stop();
                _inboxHoverTimer?.Dispose();
                _inboxHoverTimer = null;
                _linkHoverTimer?.Stop();
                _linkHoverTimer?.Dispose();
                _linkHoverTimer = null;
                _inboxCellToolTip?.Dispose();
                _topBarToolTip?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Inbox Hover ToolTips

        private void OnListViewMouseMove(object? sender, MouseEventArgs e)
        {
            if (_lvEmails.Items.Count == 0)
            {
                ResetInboxToolTip();
                return;
            }

            var hit = _lvEmails.HitTest(e.Location);
            var item = hit.Item;
            int colIndex = -1;

            if (item != null)
            {
                colIndex = GetColumnIndexAt(e.Location, item);
            }

            if (item == null || colIndex < 0 || !(item.Tag is EmailItem))
            {
                // Cursor is over empty space, headers, or borders
                ResetInboxToolTip();
                return;
            }

            // Check if cursor is still within the exact same cell
            if (item == _lastHoverItem && colIndex == _lastHoverColumn)
            {
                // If tooltip is not yet visible, restart timer on active mouse motion so it only appears when mouse rests
                if (!_isToolTipActive)
                {
                    _inboxHoverTimer?.Stop();
                    if (_inboxHoverTimer != null)
                    {
                        bool isSameMailAsActive = _lastActiveTooltipItem == item && (DateTime.UtcNow - _lastToolTipShownTime).TotalMilliseconds < 1500;
                        _inboxHoverTimer.Interval = isSameMailAsActive ? 220 : 380;
                        _inboxHoverTimer.Start();
                    }
                }
                return;
            }

            // User moved to a new cell: immediately dismiss previous tooltip so there is no lingering popup while traversing
            if (_isToolTipActive)
            {
                try
                {
                    _inboxCellToolTip?.Hide(_lvEmails);
                }
                catch { }
                _isToolTipActive = false;
            }

            // Determine if moving between columns of the SAME email or a DIFFERENT email
            bool isSameMailTransition = (_lastHoverItem == item) || (_lastActiveTooltipItem == item && (DateTime.UtcNow - _lastToolTipShownTime).TotalMilliseconds < 1500);

            _lastHoverItem = item;
            _lastHoverColumn = colIndex;

            _inboxHoverTimer?.Stop();

            if (_inboxHoverTimer != null)
            {
                // Same email column transition: gentle 220ms delay (prevents flashing when passing across Account to Sender)
                // Different email row transition: deliberate 380ms recount per email (prevents the inbox from feeling like a minefield)
                _inboxHoverTimer.Interval = isSameMailTransition ? 220 : 380;
                _inboxHoverTimer.Start();
            }
        }

        private void OnInboxHoverTimerTick(object? sender, EventArgs e)
        {
            _inboxHoverTimer?.Stop();

            if (this.IsDisposed || _lvEmails.IsDisposed || _lastHoverItem == null || _lastHoverColumn < 0)
            {
                ResetInboxToolTip();
                return;
            }

            var clientPt = _lvEmails.PointToClient(Cursor.Position);
            if (!_lvEmails.ClientRectangle.Contains(clientPt))
            {
                ResetInboxToolTip();
                return;
            }

            var hit = _lvEmails.HitTest(clientPt);
            if (hit.Item == null || hit.Item != _lastHoverItem)
            {
                ResetInboxToolTip();
                return;
            }

            int colIndex = GetColumnIndexAt(clientPt, hit.Item);
            if (colIndex != _lastHoverColumn)
            {
                ResetInboxToolTip();
                return;
            }

            if (_lastHoverItem.Tag is EmailItem email)
            {
                var screen = Screen.FromControl(_lvEmails);
                string tipText = GetToolTipTextForCell(email, _lastHoverColumn, screen.WorkingArea.Width);
                if (!string.IsNullOrWhiteSpace(tipText))
                {
                    int tipX = clientPt.X + 16;
                    int tipY = clientPt.Y + 22;

                    Point screenPt = Cursor.Position;
                    int maxTipWidth = Math.Clamp((int)(screen.WorkingArea.Width * 0.40), 300, 600);
                    if (screenPt.X + maxTipWidth > screen.WorkingArea.Right)
                    {
                        tipX = Math.Max(10, clientPt.X - maxTipWidth + 30);
                    }

                    _inboxCellToolTip.Show(tipText, _lvEmails, tipX, tipY, 10000);
                    _isToolTipActive = true;
                    _lastActiveTooltipItem = _lastHoverItem;
                    _lastToolTipShownTime = DateTime.UtcNow;
                }
                else
                {
                    ResetInboxToolTip();
                }
            }
        }

        private void OnListViewMouseLeave(object? sender, EventArgs e)
        {
            ResetInboxToolTip();
        }

        private void ResetInboxToolTip()
        {
            _inboxHoverTimer?.Stop();
            _lastHoverItem = null;
            _lastHoverColumn = -1;
            _lastActiveTooltipItem = null;
            _isToolTipActive = false;
            try
            {
                if (_inboxCellToolTip != null && _lvEmails != null && !_lvEmails.IsDisposed)
                {
                    _inboxCellToolTip.Hide(_lvEmails);
                }
            }
            catch { }
        }

        private int GetColumnIndexAt(Point pt, ListViewItem item)
        {
            var hit = _lvEmails.HitTest(pt);
            if (hit.Item == item && hit.SubItem != null)
            {
                int idx = item.SubItems.IndexOf(hit.SubItem);
                if (idx >= 0) return idx;
            }

            for (int i = 0; i < item.SubItems.Count; i++)
            {
                var bounds = item.SubItems[i].Bounds;
                if (bounds.Contains(pt))
                {
                    return i;
                }
            }

            return -1;
        }

        private string GetToolTipTextForCell(EmailItem email, int colIndex, int screenWidth)
        {
            bool isAiDisabled = _configService.Settings.IsAiDisabled;
            string raw;
            if (isAiDisabled)
            {
                raw = colIndex switch
                {
                    0 => string.IsNullOrWhiteSpace(email.Subject) ? "(No Subject)" : email.Subject.Trim(),
                    1 => GetAccountToolTipText(email),
                    2 => string.IsNullOrWhiteSpace(email.Sender) ? "(Unknown Sender)" : email.Sender.Trim(),
                    3 => GetDateToolTipText(email),
                    _ => string.Empty
                };
            }
            else
            {
                raw = colIndex switch
                {
                    0 => GetPriorityToolTipText(email),
                    1 => string.IsNullOrWhiteSpace(email.Subject) ? "(No Subject)" : email.Subject.Trim(),
                    2 => GetAccountToolTipText(email),
                    3 => string.IsNullOrWhiteSpace(email.Sender) ? "(Unknown Sender)" : email.Sender.Trim(),
                    4 => GetDateToolTipText(email),
                    _ => string.Empty
                };
            }
            return WrapTextForToolTip(raw, screenWidth);
        }

        private static string WrapTextForToolTip(string text, int screenWidth)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            int maxLineLength = Math.Clamp((int)(screenWidth * 0.048), 45, 85);
            if (text.Length <= maxLineLength || text.Contains('\n')) return text;

            var words = text.Split(' ');
            var sb = new StringBuilder();
            int currentLineLen = 0;

            foreach (var word in words)
            {
                if (currentLineLen + word.Length + 1 > maxLineLength)
                {
                    sb.AppendLine();
                    currentLineLen = 0;
                }
                else if (currentLineLen > 0)
                {
                    sb.Append(' ');
                    currentLineLen++;
                }
                sb.Append(word);
                currentLineLen += word.Length;
            }

            return sb.ToString();
        }

        private static string GetPriorityToolTipText(EmailItem email)
        {
            if (email.Priority.HasValue)
            {
                return email.Priority.Value switch
                {
                    1 => "⚡ Priority 1 (High / Urgent)",
                    2 => "⚡ Priority 2 (Normal)",
                    3 => "⚡ Priority 3 (Low / Newsletter)",
                    _ => $"⚡ Priority {email.Priority.Value}"
                };
            }

            if (email.Status == SummaryState.Summarizing)
            {
                return "⏳ Summarizing: Generating AI summary...";
            }

            return "Priority: Not evaluated";
        }

        private static string GetAccountToolTipText(EmailItem email)
        {
            string name = email.AccountName?.Trim() ?? string.Empty;
            string addr = email.AccountEmail?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(addr) && !string.Equals(name, addr, StringComparison.OrdinalIgnoreCase))
            {
                return $"📬 {name} ({addr})";
            }

            return !string.IsNullOrEmpty(name) ? $"📬 {name}" : (!string.IsNullOrEmpty(addr) ? $"📬 {addr}" : "Default Account");
        }

        private static string GetDateToolTipText(EmailItem email)
        {
            try
            {
                return $"📅 {email.Date.LocalDateTime:dddd, MMMM d, yyyy  HH:mm:ss}";
            }
            catch
            {
                return $"📅 {email.DateString}";
            }
        }

        #endregion

        #region Link Hover ToolTips

        private void UpdateEmailLinkSpans(EmailItem email)
        {
            _currentEmailLinkSpans.Clear();
            string visibleText = _rtbEmailBody.Text;
            if (string.IsNullOrEmpty(visibleText)) return;

            if (email.ExtractedLinks != null)
            {
                int searchStart = 0;
                foreach (var link in email.ExtractedLinks)
                {
                    if (string.IsNullOrWhiteSpace(link.Text) || string.IsNullOrWhiteSpace(link.Url)) continue;

                    int found = visibleText.IndexOf(link.Text, searchStart, StringComparison.OrdinalIgnoreCase);
                    if (found >= 0)
                    {
                        _currentEmailLinkSpans.Add((found, link.Text.Length, link.Url));
                        searchStart = found + Math.Max(1, link.Text.Length);
                    }
                    else
                    {
                        int fallback = visibleText.IndexOf(link.Text, 0, StringComparison.OrdinalIgnoreCase);
                        if (fallback >= 0 && !_currentEmailLinkSpans.Any(s => s.Start == fallback && s.Length == link.Text.Length))
                        {
                            _currentEmailLinkSpans.Add((fallback, link.Text.Length, link.Url));
                        }
                    }
                }
            }

            var urlMatches = System.Text.RegularExpressions.Regex.Matches(visibleText, @"https?://[^\s<>""'{}|\^\[\]`]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match m in urlMatches)
            {
                if (!_currentEmailLinkSpans.Any(s => s.Start == m.Index && s.Length == m.Length))
                {
                    _currentEmailLinkSpans.Add((m.Index, m.Length, m.Value));
                }
            }
        }

        private void OnEmailBodyMouseMove(object? sender, MouseEventArgs e)
        {
            if (_currentEmailLinkSpans.Count == 0 || string.IsNullOrEmpty(_rtbEmailBody.Text))
            {
                ResetLinkToolTip();
                return;
            }

            int charIndex = _rtbEmailBody.GetCharIndexFromPosition(e.Location);
            if (charIndex < 0 || charIndex >= _rtbEmailBody.TextLength)
            {
                ResetLinkToolTip();
                return;
            }

            Point charPt = _rtbEmailBody.GetPositionFromCharIndex(charIndex);
            if (Math.Abs(e.Location.Y - charPt.Y) > 24 || e.Location.X < charPt.X - 15)
            {
                ResetLinkToolTip();
                return;
            }

            var span = _currentEmailLinkSpans.FirstOrDefault(s => charIndex >= s.Start && charIndex < s.Start + s.Length);
            if (span.Length > 0 && !string.IsNullOrWhiteSpace(span.Url))
            {
                if (span.Url == _lastHoverLinkUrl)
                {
                    return;
                }

                _lastHoverLinkUrl = span.Url;
                _linkHoverTimer?.Stop();
                if (_linkHoverTimer != null)
                {
                    _linkHoverTimer.Interval = 250;
                    _linkHoverTimer.Start();
                }
            }
            else
            {
                ResetLinkToolTip();
            }
        }

        private void OnLinkHoverTimerTick(object? sender, EventArgs e)
        {
            _linkHoverTimer?.Stop();
            if (string.IsNullOrWhiteSpace(_lastHoverLinkUrl) || this.IsDisposed || _rtbEmailBody.IsDisposed)
            {
                ResetLinkToolTip();
                return;
            }

            var clientPt = _rtbEmailBody.PointToClient(Cursor.Position);
            if (!_rtbEmailBody.ClientRectangle.Contains(clientPt))
            {
                ResetLinkToolTip();
                return;
            }

            var screen = Screen.FromControl(_rtbEmailBody);
            string formattedTip = FormatUrlForToolTip(_lastHoverLinkUrl, screen.WorkingArea.Width);

            int tipX = clientPt.X + 16;
            int tipY = clientPt.Y + 22;

            Point screenPt = Cursor.Position;
            int maxTipWidth = Math.Clamp((int)(screen.WorkingArea.Width * 0.42), 320, 620);
            if (screenPt.X + maxTipWidth > screen.WorkingArea.Right)
            {
                tipX = Math.Max(10, clientPt.X - maxTipWidth + 30);
            }

            _inboxCellToolTip.Show(formattedTip, _rtbEmailBody, tipX, tipY, 10000);
        }

        private static string FormatUrlForToolTip(string url, int screenWidth)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            // Dynamically calculate max characters per line proportional to screen width (45 to 80 chars)
            int maxLineLength = Math.Clamp((int)(screenWidth * 0.042), 45, 80);
            if (url.Length <= maxLineLength)
            {
                return "🔗 " + url;
            }

            var lines = new List<string>();
            string remaining = url;

            while (remaining.Length > maxLineLength)
            {
                int minSplit = Math.Max(18, maxLineLength - 25);
                int breakIdx = -1;

                char[] delimiters = new[] { '&', '?', '/', '#', '=', ';' };
                foreach (char d in delimiters)
                {
                    int idx = remaining.LastIndexOf(d, maxLineLength, maxLineLength - minSplit);
                    if (idx > minSplit)
                    {
                        if (d == '&' || d == '?')
                        {
                            breakIdx = idx; // Break before '&' or '?' so it starts next line
                        }
                        else
                        {
                            breakIdx = idx + 1; // Include '/' or '=' in current line
                        }
                        break;
                    }
                }

                if (breakIdx <= 0)
                {
                    breakIdx = maxLineLength;
                }

                string part = remaining.Substring(0, breakIdx).TrimEnd();
                lines.Add(lines.Count == 0 ? "🔗 " + part : "   " + part);
                remaining = remaining.Substring(breakIdx).TrimStart();
            }

            if (remaining.Length > 0)
            {
                lines.Add(lines.Count == 0 ? "🔗 " + remaining : "   " + remaining);
            }

            return string.Join(Environment.NewLine, lines);
        }

        private void ResetLinkToolTip()
        {
            _linkHoverTimer?.Stop();
            if (_lastHoverLinkUrl != null)
            {
                _lastHoverLinkUrl = null;
                try
                {
                    if (_inboxCellToolTip != null && _rtbEmailBody != null && !_rtbEmailBody.IsDisposed)
                    {
                        _inboxCellToolTip.Hide(_rtbEmailBody);
                    }
                }
                catch { }
            }
        }

        private void OnEmailBodyMouseLeave(object? sender, EventArgs e)
        {
            ResetLinkToolTip();
        }

        #region Email Attachments

        private void UpdateEmailAttachments(EmailItem email)
        {
            _pnlAttachments.SuspendLayout();
            _pnlAttachments.Controls.Clear();

            if (!email.HasAttachments)
            {
                _pnlAttachments.Visible = false;
                _pnlAttachments.ResumeLayout();
                return;
            }

            long totalBytes = email.Attachments.Sum(a => a.FileSizeBytes);
            string totalFormatted = totalBytes > 0
                ? (totalBytes < 1024 * 1024 ? $"{totalBytes / 1024.0:F1} KB" : $"{totalBytes / (1024.0 * 1024.0):F1} MB")
                : "";

            string sizeSuffix = !string.IsNullOrEmpty(totalFormatted) ? $" ({totalFormatted})" : "";
            float scale = this.DeviceDpi / 96f;

            _lblAttachmentsTitle.Text = $"📎 {Lang.T(StringKeys.InboxAttachmentsTitle)} {email.Attachments.Count}{sizeSuffix}:";
            _lblAttachmentsTitle.Margin = new Padding(0, (int)(4 * scale), (int)(8 * scale), 0);
            _pnlAttachments.Controls.Add(_lblAttachmentsTitle);

            foreach (var att in email.Attachments)
            {
                var btnAtt = new Button
                {
                    Text = $"{att.GetFileIcon()}  {att.FileName} ({att.FormattedSize})   ⬇",
                    AutoSize = true,
                    Height = (int)(25 * scale),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White,
                    ForeColor = Color.FromArgb(20, 50, 100),
                    Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0, 0, (int)(6 * scale), 0)
                };
                btnAtt.FlatAppearance.BorderColor = Color.FromArgb(195, 212, 235);
                btnAtt.FlatAppearance.BorderSize = 1;
                btnAtt.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 242, 255);
                btnAtt.Click += async (s, e) => await DownloadSingleAttachmentAsync(email, att);

                _pnlAttachments.Controls.Add(btnAtt);
            }

            if (email.Attachments.Count > 1)
            {
                var btnSaveAll = new Button
                {
                    Text = "⬇ " + Lang.T(StringKeys.InboxBtnDownloadAttachments),
                    AutoSize = true,
                    Height = (int)(25 * scale),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(235, 244, 255),
                    ForeColor = Color.FromArgb(10, 70, 150),
                    Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    Margin = new Padding((int)(4 * scale), 0, 0, 0)
                };
                btnSaveAll.FlatAppearance.BorderColor = Color.FromArgb(160, 195, 240);
                btnSaveAll.FlatAppearance.BorderSize = 1;
                btnSaveAll.FlatAppearance.MouseOverBackColor = Color.FromArgb(215, 232, 255);
                btnSaveAll.Click += OnSaveAllAttachmentsClick;

                _pnlAttachments.Controls.Add(btnSaveAll);
            }

            _pnlAttachments.ResumeLayout();
            _pnlAttachments.Visible = true;
        }

        private async Task DownloadSingleAttachmentAsync(EmailItem email, EmailAttachmentInfo att)
        {
            var account = _configService.GetAccounts().FirstOrDefault(a =>
                string.Equals(a.Email, email.AccountEmail, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Name, email.AccountName, StringComparison.OrdinalIgnoreCase));

            if (account == null)
            {
                MessageBox.Show(Lang.Format(StringKeys.InboxAccountNotFound, email.AccountName, email.AccountEmail), Lang.T(StringKeys.InboxDownloadError), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                FileName = att.FileName,
                Title = $"Save {att.FileName}",
                Filter = "All Files (*.*)|*.*",
                InitialDirectory = _configService.Settings.GetEffectiveAttachmentDownloadPath()
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            string destPath = sfd.FileName;
            StatusUpdated?.Invoke($"Downloading '{att.FileName}' from {account.Name}...", "IMAP Fetch");

            try
            {
                var (success, msg) = await _imapService.DownloadAttachmentAsync(
                    account,
                    email.UniqueId,
                    att.PartIndex,
                    att.FileName,
                    destPath,
                    _logger);

                if (success)
                {
                    StatusUpdated?.Invoke(Lang.Format(StringKeys.InboxDownloadedSingleStatus, att.FileName), Lang.T(StringKeys.StatusReady));
                    var res = MessageBox.Show(Lang.Format(StringKeys.InboxDownloadedSingleToast, att.FileName, destPath), Lang.T(StringKeys.InboxDownloadComplete), MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (res == DialogResult.Yes)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = $"/select,\"{destPath}\"",
                                UseShellExecute = true
                            });
                        }
                        catch { }
                    }
                }
                else
                {
                    StatusUpdated?.Invoke(Lang.T(StringKeys.InboxDownloadFailedStatus), Lang.T(StringKeys.StatusReady));
                    MessageBox.Show(msg, Lang.T(StringKeys.InboxDownloadFailed), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke(Lang.T(StringKeys.InboxDownloadFailedStatus), Lang.T(StringKeys.StatusReady));
                MessageBox.Show(Lang.Format(StringKeys.InboxDownloadError, ex.Message), Lang.T(StringKeys.CommonError), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnSaveAllAttachmentsClick(object? sender, EventArgs e)
        {
            var email = GetCurrentPreviewEmail();
            if (email == null || !email.HasAttachments) return;

            var account = _configService.GetAccounts().FirstOrDefault(a =>
                string.Equals(a.Email, email.AccountEmail, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Name, email.AccountName, StringComparison.OrdinalIgnoreCase));

            if (account == null)
            {
                MessageBox.Show(Lang.Format(StringKeys.InboxAccountNotFound, email.AccountName, email.AccountEmail), Lang.T(StringKeys.InboxDownloadError), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var fbd = new FolderBrowserDialog
            {
                Description = Lang.T(StringKeys.SettingsDownloadPathSelectDesc),
                UseDescriptionForTitle = true,
                SelectedPath = _configService.Settings.GetEffectiveAttachmentDownloadPath()
            };

            if (fbd.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(fbd.SelectedPath)) return;

            string destFolder = fbd.SelectedPath;
            int successCount = 0;
            int total = email.Attachments.Count;

            var btn = sender as Button;
            if (btn != null) btn.Enabled = false;
            StatusUpdated?.Invoke($"Downloading {total} attachments...", "IMAP Fetch");

            try
            {
                foreach (var att in email.Attachments)
                {
                    string destPath = Path.Combine(destFolder, att.FileName);
                    int copy = 1;
                    while (File.Exists(destPath))
                    {
                        string nameWithoutExt = Path.GetFileNameWithoutExtension(att.FileName);
                        string ext = Path.GetExtension(att.FileName);
                        destPath = Path.Combine(destFolder, $"{nameWithoutExt} ({copy++}){ext}");
                    }

                    var (success, _) = await _imapService.DownloadAttachmentAsync(
                        account,
                        email.UniqueId,
                        att.PartIndex,
                        att.FileName,
                        destPath,
                        _logger);

                    if (success) successCount++;
                }

                StatusUpdated?.Invoke(Lang.Format(StringKeys.InboxSavedAttachmentsStatus, successCount, total), Lang.T(StringKeys.StatusReady));
                var res = MessageBox.Show(Lang.Format(StringKeys.InboxSavedAttachmentsToast, successCount, total, destFolder), Lang.T(StringKeys.InboxDownloadComplete), MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (res == DialogResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = destFolder,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Lang.Format(StringKeys.InboxDownloadError, ex.Message), Lang.T(StringKeys.CommonError), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (btn != null) btn.Enabled = true;
            }
        }

        #endregion

        #endregion
    }
}
