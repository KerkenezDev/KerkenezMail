using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EmailSummarizer.Models;
using EmailSummarizer.Services;

namespace EmailSummarizer.UI.Tabs
{
    public class SummariesView : UserControl
    {
        private readonly ConfigService _configService;
        private readonly ImapService _imapService;
        private readonly LlamaServerManager _llamaManager;
        private readonly LlmSummarizerService _llmService;
        private readonly IProgress<string> _logger;

        private CancellationTokenSource? _cts;
        private readonly List<EmailItem> _emails = new List<EmailItem>();
        private readonly List<EmailItem> _selectedEmailsOrder = new List<EmailItem>();

        // Triage State & Debouncing
        private enum TriageActionType { Archive, Delete }
        private class PendingTriageItem
        {
            public EmailItem Email { get; set; } = null!;
            public TriageActionType Action { get; set; }
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
        private Button _btnArchive = null!;
        private Button _btnDelete = null!;
        private ComboBox _cboAccountFilter = null!;
        private TextBox _txtSearch = null!;
        private ListView _lvEmails = null!;
        private TextBox _txtSummary = null!;
        private TextBox _txtEmailBody = null!;
        private Label _lblEmailMeta = null!;
        private Label _lblEmailSubject = null!;
        private Label _lblInboxHeader = null!;
        private ProgressBar _progressBar = null!;
        private SplitContainer _mainSplit = null!;
        private int _inboxPanelWidth = 440;
        private bool _isInitialSplitSet = false;

        public event Action<string, string>? StatusUpdated;

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
                Text = "🔄  Refresh Inbox",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(10 * scale), (int)(6 * scale), (int)(10 * scale), (int)(6 * scale)),
                Margin = new Padding(0, 0, (int)(8 * scale), 0),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnRefresh.Click += async (s, e) => await FetchAndAutoSummarizeAsync();

            _btnCopySummary = new Button
            {
                Text = "📋 Copy Summary",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(10 * scale), (int)(6 * scale), (int)(10 * scale), (int)(6 * scale)),
                Margin = new Padding(0, 0, (int)(8 * scale), 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnCopySummary.Click += OnCopySummaryClick;

            _btnExport = new Button
            {
                Text = "💾 Export...",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding((int)(10 * scale), (int)(6 * scale), (int)(10 * scale), (int)(6 * scale)),
                Margin = new Padding(0, 0, (int)(8 * scale), 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnExport.Click += OnExportClick;

            // Stacked Archive and Delete half-height square buttons
            var pnlTriageButtons = new Panel
            {
                Width = (int)(32 * scale),
                Height = btnH,
                Margin = new Padding(0, 0, (int)(14 * scale), 0),
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

            var toolTip = new ToolTip();
            toolTip.SetToolTip(_btnArchive, "Archive selected email(s)");
            toolTip.SetToolTip(_btnDelete, "Delete selected email(s)");

            pnlTriageButtons.Controls.Add(_btnDelete);
            pnlTriageButtons.Controls.Add(_btnArchive);

            var lblAccount = new Label
            {
                Text = "Account:",
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
                PlaceholderText = "Search emails..."
            };
            _txtSearch.TextChanged += (s, e) => ApplyFilter();

            actionsFlow.Controls.Add(_btnRefresh);
            actionsFlow.Controls.Add(_btnCopySummary);
            actionsFlow.Controls.Add(_btnExport);
            actionsFlow.Controls.Add(pnlTriageButtons);
            actionsFlow.Controls.Add(lblAccount);
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
            var middleSplit = new SplitContainer
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

            _lblEmailSubject = new Label
            {
                Text = "Subject: (No email selected)",
                Dock = DockStyle.Top,
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 20, 20),
                Padding = new Padding(0, 0, 0, 4)
            };

            _lblEmailMeta = new Label
            {
                Text = "From: -   •   Date: -   •   Account: -",
                Dock = DockStyle.Top,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(100, 100, 100)
            };

            metaHeader.Controls.Add(_lblEmailMeta);
            metaHeader.Controls.Add(_lblEmailSubject);

            _txtEmailBody = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.75F),
                PlaceholderText = "Select an email from the inbox list on the right to view its content."
            };

            emailViewerPanel.Controls.Add(_txtEmailBody);
            emailViewerPanel.Controls.Add(metaHeader);
            middleSplit.Panel1.Controls.Add(emailViewerPanel);

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

            var lblSummaryTitle = new Label
            {
                Text = "✨ AI Executive Summary",
                Dock = DockStyle.Left,
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204)
            };

            var lblSummaryHint = new Label
            {
                Text = "(Generated by local LLM in VRAM)",
                Dock = DockStyle.Right,
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(120, 120, 120)
            };

            summaryHeader.Controls.Add(lblSummaryTitle);
            summaryHeader.Controls.Add(lblSummaryHint);

            _txtSummary = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(245, 250, 255),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                PlaceholderText = "AI summary will appear here..."
            };

            summaryCard.Controls.Add(_txtSummary);
            summaryCard.Controls.Add(summaryHeader);
            middleSplit.Panel2.Controls.Add(summaryCard);

            _mainSplit.Panel1.Controls.Add(middleSplit);

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
            _lvEmails.Columns.Add("⚡", 44);
            _lvEmails.Columns.Add("Subject", 190);
            _lvEmails.Columns.Add("Account", 95);
            _lvEmails.Columns.Add("From", 120);
            _lvEmails.Columns.Add("Date", 85);
            _lvEmails.ItemSelectionChanged += OnEmailItemSelectionChanged;
            _lvEmails.SelectedIndexChanged += OnEmailSelected;

            rightListPanel.Controls.Add(_lvEmails);
            rightListPanel.Controls.Add(_lblInboxHeader);
            _mainSplit.Panel2.Controls.Add(rightListPanel);

            this.Controls.Add(_mainSplit);
            this.Controls.Add(_topPanel);

            this.Load += (s, e) => AdjustSplitter();
            this.Resize += (s, e) => AdjustSplitter();
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

            _cboAccountFilter.Items.Clear();
            _cboAccountFilter.Items.Add("All Accounts");

            foreach (var acc in _configService.GetAccounts())
            {
                _cboAccountFilter.Items.Add(acc.Name);
            }

            if (!string.IsNullOrEmpty(currentSelection) && _cboAccountFilter.Items.Contains(currentSelection))
            {
                _cboAccountFilter.SelectedItem = currentSelection;
            }
            else
            {
                _cboAccountFilter.SelectedIndex = 0;
            }
        }

        public async Task FetchAndAutoSummarizeAsync()
        {
            if (_btnRefresh.Enabled == false) return;

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            _btnRefresh.Enabled = false;
            _progressBar.Visible = true;

            var settings = _configService.Settings;
            var accounts = _configService.GetAccounts();
            if (accounts.Count == 0)
            {
                _logger.Report("\r\n" + new string('═', 60));
                _logger.Report("[!] No email accounts configured. Please add an account in the Accounts tab.");
                StatusUpdated?.Invoke("No accounts configured", "Ready");
                _btnRefresh.Enabled = true;
                _progressBar.Visible = false;
                return;
            }

            string backendName = settings.GetBackendDisplayName();

            StatusUpdated?.Invoke("Syncing inboxes...", "Active");
            _logger.Report("\r\n" + new string('═', 60));
            _logger.Report($"[*] Fast-syncing all accounts with AI backend [{backendName}]...");

            _emails.Clear();
            PopulateListView();

            try
            {
                // 1. Launch LLM Server in parallel background task ONLY if using local llama.cpp
                Task<bool>? serverTask = null;
                if (string.Equals(settings.AiBackend, "LlamaCpp", StringComparison.OrdinalIgnoreCase) && settings.AutoStartLlamaServer)
                {
                    serverTask = _llamaManager.StartAsync(
                        settings.LlamaModelPath,
                        settings.LlamaServerPort,
                        settings.LlamaGpuLayers,
                        logger: _logger,
                        ct: ct);
                }

                // 2. Concurrently fetch all IMAP accounts in parallel tasks, streaming emails to UI as they arrive!
                var fetchTask = _imapService.FetchAllAccountsParallelAsync(
                    accounts,
                    settings,
                    _logger,
                    onEmailFetched: emailItem =>
                    {
                        if (this.IsDisposed || !this.IsHandleCreated) return;

                        try
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                if (this.IsDisposed || !this.IsHandleCreated) return;

                                _emails.Add(emailItem);
                                AddEmailItemToListView(emailItem);

                                if (_lvEmails.SelectedItems.Count == 0 && _lvEmails.Items.Count > 0)
                                {
                                    _lvEmails.Items[0].Selected = true;
                                }

                                if (!emailItem.IsRead)
                                {
                                    _ = SummarizeUnreadEmailInBackgroundAsync(emailItem, settings, serverTask, ct);
                                }
                            }));
                        }
                        catch { }
                    },
                    ct: ct);

                await fetchTask;
                if (serverTask != null) await serverTask;

                int unreadCount = _emails.Count(e => !e.IsRead);
                _logger.Report($"[✓] Parallel sync complete. Loaded {_emails.Count} total email(s) ({unreadCount} unread).");
                string backendMetric = string.Equals(settings.AiBackend, "LlamaCpp", StringComparison.OrdinalIgnoreCase) 
                    ? "Model Loaded in VRAM" 
                    : (string.Equals(settings.AiBackend, "Ollama", StringComparison.OrdinalIgnoreCase) ? "Ollama Active" : "Cloud Active");
                StatusUpdated?.Invoke($"Ready • {_emails.Count} emails in inbox ({unreadCount} unread)", backendMetric);
            }
            catch (OperationCanceledException)
            {
                StatusUpdated?.Invoke("Operation cancelled", "Ready");
                _logger.Report("[!] Inbox sync cancelled.");
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke($"Error: {ex.Message}", "Error");
                _logger.Report($"[!] Sync error: {ex.Message}");
            }
            finally
            {
                _btnRefresh.Enabled = true;
                _progressBar.Visible = false;
            }
        }

        private async Task SummarizeUnreadEmailInBackgroundAsync(EmailItem email, AppSettings settings, Task<bool>? serverTask, CancellationToken ct)
        {
            try
            {
                if (serverTask != null)
                {
                    await serverTask;
                }

                if (ct.IsCancellationRequested || this.IsDisposed) return;

                email.Status = SummaryState.Summarizing;
                string summary = await _llmService.SummarizeEmailAsync(email, settings, ct);
                email.Summary = summary;
                email.Status = SummaryState.Completed;

                _logger.Report($"[✓] Background summary generated for: \"{email.Subject}\" (Priority {email.Priority ?? 2})");

                if (this.IsDisposed || !this.IsHandleCreated) return;

                this.BeginInvoke(new Action(() =>
                {
                    if (this.IsDisposed || !this.IsHandleCreated) return;

                    UpdateListViewItemForEmail(email);

                    if (_lvEmails.SelectedItems.Count > 0 && _lvEmails.SelectedItems[0].Tag == email)
                    {
                        DisplayEmail(email);
                    }
                }));
            }
            catch
            {
                email.Status = SummaryState.Failed;
            }
        }

        private ListViewItem CreateListViewItemForEmail(EmailItem email, ListViewGroup group)
        {
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

            var item = new ListViewItem(priText, group)
            {
                UseItemStyleForSubItems = false,
                Tag = email,
                ForeColor = priColor,
                Font = priFont
            };

            string subjectPrefix = email.IsArchived ? "📥 " : (email.IsRead ? "" : "● ");
            var subSubject = item.SubItems.Add(subjectPrefix + email.Subject);
            var subAccount = item.SubItems.Add(email.AccountName);
            var subSender = item.SubItems.Add(email.Sender);
            var subDate = item.SubItems.Add(email.DateString);

            if (email.IsArchived)
            {
                var archivedColor = Color.FromArgb(150, 155, 165);
                var regFont = new Font("Segoe UI", 8.75F, FontStyle.Regular);
                subSubject.ForeColor = archivedColor; subSubject.Font = regFont;
                subAccount.ForeColor = archivedColor; subAccount.Font = regFont;
                subSender.ForeColor = archivedColor; subSender.Font = regFont;
                subDate.ForeColor = archivedColor; subDate.Font = regFont;
            }
            else if (email.IsRead)
            {
                var readColor = Color.FromArgb(130, 135, 145);
                var regFont = new Font("Segoe UI", 8.75F, FontStyle.Regular);
                subSubject.ForeColor = readColor; subSubject.Font = regFont;
                subAccount.ForeColor = readColor; subAccount.Font = regFont;
                subSender.ForeColor = readColor; subSender.Font = regFont;
                subDate.ForeColor = readColor; subDate.Font = regFont;
            }
            else
            {
                var unreadColor = Color.FromArgb(15, 15, 15);
                var boldFont = new Font("Segoe UI", 9F, FontStyle.Bold);
                var regFont = new Font("Segoe UI", 8.75F, FontStyle.Regular);
                subSubject.ForeColor = unreadColor; subSubject.Font = boldFont;
                subAccount.ForeColor = Color.FromArgb(70, 75, 85); subAccount.Font = regFont;
                subSender.ForeColor = Color.FromArgb(60, 65, 75); subSender.Font = regFont;
                subDate.ForeColor = Color.FromArgb(90, 95, 105); subDate.Font = regFont;
            }

            return item;
        }

        private void AddEmailItemToListView(EmailItem email)
        {
            var filterAccount = _cboAccountFilter.SelectedItem?.ToString()?.Trim();
            string search = _txtSearch.Text.Trim();

            if (!string.IsNullOrEmpty(filterAccount) && 
                !string.Equals(filterAccount, "All Accounts", StringComparison.OrdinalIgnoreCase) && 
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
            _lblInboxHeader.Text = $"Inbox ({_emails.Count} emails, {unreadCount} unread)";
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

            _lvEmails.BeginUpdate();
            _lvEmails.Items.Clear();
            _lvEmails.Groups.Clear();

            var filterAccount = _cboAccountFilter.SelectedItem?.ToString()?.Trim();
            string search = _txtSearch.Text.Trim();

            var visibleEmails = _emails.Where(e =>
            {
                if (!string.IsNullOrEmpty(filterAccount) && 
                    !string.Equals(filterAccount, "All Accounts", StringComparison.OrdinalIgnoreCase) && 
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
            _lblInboxHeader.Text = $"Inbox ({visibleEmails.Count} emails, {unreadCount} unread)";

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
                _txtEmailBody.Clear();
                _lblEmailSubject.Text = "Subject: (No email selected)";
                _lblEmailMeta.Text = "From: -   •   Date: -   •   Account: -";
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
                _selectedEmailsOrder.Clear();
                _txtSummary.Clear();
                _txtEmailBody.Clear();
                _lblEmailSubject.Text = "Subject: (No email selected)";
                _lblEmailMeta.Text = "From: -   •   Date: -   •   Account: -";
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

            if (string.IsNullOrWhiteSpace(email.Summary) || 
                email.Summary.Contains("Loading model", StringComparison.OrdinalIgnoreCase) ||
                email.Summary.StartsWith("(LLM Error", StringComparison.OrdinalIgnoreCase) ||
                email.Summary.StartsWith("(Could not reach LLM", StringComparison.OrdinalIgnoreCase))
            {
                _txtSummary.Text = "✨ Generating AI summary for this email...";
                StatusUpdated?.Invoke($"Summarizing \"{email.Subject}\"...", "In Use (GPU)");

                var settings = _configService.Settings;
                if (string.Equals(settings.AiBackend, "LlamaCpp", StringComparison.OrdinalIgnoreCase) && settings.AutoStartLlamaServer)
                {
                    await _llamaManager.StartAsync(settings.LlamaModelPath, settings.LlamaServerPort, settings.LlamaGpuLayers, logger: _logger);
                }

                string summary = await _llmService.SummarizeEmailAsync(email, settings);
                email.Summary = summary;
                email.Status = SummaryState.Completed;

                UpdateListViewItemForEmail(email);

                if (_lvEmails.SelectedItems.Count > 0 && GetCurrentPreviewEmail() == email)
                {
                    DisplayEmail(email);
                }

                string backendMetric = string.Equals(settings.AiBackend, "LlamaCpp", StringComparison.OrdinalIgnoreCase) 
                    ? "Model Loaded in VRAM" 
                    : (string.Equals(settings.AiBackend, "Ollama", StringComparison.OrdinalIgnoreCase) ? "Ollama Active" : "Cloud Active");
                StatusUpdated?.Invoke("Summary ready", backendMetric);
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

        private void DisplayEmail(EmailItem email)
        {
            string readTag = email.IsRead ? "[Read]" : "[Unread]";
            if (email.IsArchived) readTag = "[Archived] • " + readTag;

            string priTag = email.Priority.HasValue
                ? $"   •   Priority: {email.Priority.Value} ({(email.Priority.Value == 1 ? "High" : email.Priority.Value == 2 ? "Normal" : "Low")})"
                : "";

            _lblEmailSubject.Text = $"Subject: {email.Subject}";
            _lblEmailMeta.Text = $"From: {email.Sender}   •   Date: {email.DateString}   •   Account: {email.AccountName}{priTag}   •   {readTag}";

            string summaryText = string.IsNullOrWhiteSpace(email.Summary) 
                ? "✨ Generating AI summary for this email..." 
                : email.Summary;

            if (email.IsArchived && !summaryText.StartsWith("📥 ", StringComparison.OrdinalIgnoreCase) && !summaryText.StartsWith("[Archived] ", StringComparison.OrdinalIgnoreCase))
            {
                summaryText = "📥 " + summaryText;
            }

            _txtSummary.Text = summaryText;
            _txtEmailBody.Text = email.CleanBody;
        }

        private void UpdateListViewItemForEmail(EmailItem email)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            foreach (ListViewItem item in _lvEmails.Items)
            {
                if (item.Tag == email)
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
                        string subjectPrefix = email.IsArchived ? "📥 " : (email.IsRead ? "" : "● ");
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
            }

            var previewEmail = GetCurrentPreviewEmail();
            if (previewEmail != null)
            {
                DisplayEmail(previewEmail);
            }

            if (selected.Count > 1)
            {
                var task = ExecuteImapTriageAsync(selected.Select(em => new PendingTriageItem { Email = em, Action = TriageActionType.Archive }).ToList());
                TrackInFlightTask(task);
            }
            else
            {
                QueueSingleTriage(selected[0], TriageActionType.Archive);
            }
        }

        private void OnDeleteClick(object? sender, EventArgs e)
        {
            var selected = GetSelectedEmailItems();
            if (selected.Count == 0) return;

            _lvEmails.BeginUpdate();
            foreach (var email in selected)
            {
                _emails.Remove(email);
                _selectedEmailsOrder.Remove(email);

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
            _lblInboxHeader.Text = $"Inbox ({_emails.Count} emails, {unreadCount} unread)";

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
                _txtSummary.Clear();
                _txtEmailBody.Clear();
                _lblEmailSubject.Text = "Subject: (No email selected)";
                _lblEmailMeta.Text = "From: -   •   Date: -   •   Account: -";
            }

            if (selected.Count > 1)
            {
                var task = ExecuteImapTriageAsync(selected.Select(em => new PendingTriageItem { Email = em, Action = TriageActionType.Delete }).ToList());
                TrackInFlightTask(task);
            }
            else
            {
                QueueSingleTriage(selected[0], TriageActionType.Delete);
            }
        }

        private void QueueSingleTriage(EmailItem email, TriageActionType action)
        {
            lock (_triageLock)
            {
                _pendingSingleTriage.RemoveAll(x => x.Email.UniqueId == email.UniqueId && string.Equals(x.Email.AccountEmail, email.AccountEmail, StringComparison.OrdinalIgnoreCase));
                _pendingSingleTriage.Add(new PendingTriageItem { Email = email, Action = action });

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
            var deleteEmails = items.Where(i => i.Action == TriageActionType.Delete).Select(i => i.Email).ToList();
            var archiveEmails = items.Where(i => i.Action == TriageActionType.Archive).Select(i => i.Email).ToList();

            var tasks = new List<Task>();

            if (deleteEmails.Count > 0)
            {
                tasks.Add(_imapService.DeleteEmailsBatchAsync(deleteEmails, accounts, _logger));
            }

            if (archiveEmails.Count > 0)
            {
                tasks.Add(_imapService.ArchiveEmailsBatchAsync(archiveEmails, accounts, _logger));
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
                MessageBox.Show("Summary copied to clipboard!", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show("All summaries copied to clipboard in Markdown format!", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("No summary available to copy.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OnExportClick(object? sender, EventArgs e)
        {
            if (!_emails.Any())
            {
                MessageBox.Show("No emails available to export.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                        sb.AppendLine(email.CleanBody);
                        sb.AppendLine("```");
                        sb.AppendLine("</details>");
                        sb.AppendLine();
                        sb.AppendLine("---");
                        sb.AppendLine();
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Successfully exported to {Path.GetFileName(sfd.FileName)}!", "Export Succeeded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export file: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
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
            }
            base.Dispose(disposing);
        }
    }
}
