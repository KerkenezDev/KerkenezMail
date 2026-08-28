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

        // Controls
        private Panel _topPanel = null!;
        private Button _btnRefresh = null!;
        private Button _btnCopySummary = null!;
        private Button _btnExport = null!;
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
                Padding = new Padding((int)(14 * scale), (int)(6 * scale), (int)(14 * scale), (int)(6 * scale)),
                Margin = new Padding(0, 0, (int)(10 * scale), 0),
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
                Padding = new Padding((int)(12 * scale), (int)(6 * scale), (int)(12 * scale), (int)(6 * scale)),
                Margin = new Padding(0, 0, (int)(10 * scale), 0),
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
                Padding = new Padding((int)(12 * scale), (int)(6 * scale), (int)(12 * scale), (int)(6 * scale)),
                Margin = new Padding(0, 0, (int)(18 * scale), 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnExport.Click += OnExportClick;

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
            actionsFlow.Controls.Add(lblAccount);
            actionsFlow.Controls.Add(_cboAccountFilter);
            actionsFlow.Controls.Add(_txtSearch);

            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 3,
                Visible = false,
                Style = ProgressBarStyle.Marquee
            };

            _topPanel.Controls.Add(actionsFlow);
            _topPanel.Controls.Add(_progressBar);

            // 2. Main SplitContainer (Left/Middle = Email Body + AI Summary, Right = Grouped Inbox List)
            _mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel2,
                SplitterWidth = 6,
                Padding = new Padding(14, 12, 14, 12)
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
                ShowGroups = true
            };
            _lvEmails.Columns.Add("Subject", 210);
            _lvEmails.Columns.Add("Account", 110);
            _lvEmails.Columns.Add("From", 140);
            _lvEmails.Columns.Add("Date", 95);
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
            if (_mainSplit.Width > 500)
            {
                int desiredRightWidth = Math.Min(560, Math.Max(380, (int)(_mainSplit.Width * 0.38)));
                int targetDistance = _mainSplit.Width - desiredRightWidth;
                if (targetDistance > 200 && targetDistance < _mainSplit.Width - 100)
                {
                    _mainSplit.SplitterDistance = targetDistance;
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

            string modelName = string.IsNullOrWhiteSpace(settings.LlamaModelPath) 
                ? "Not Selected" 
                : Path.GetFileName(settings.LlamaModelPath);

            StatusUpdated?.Invoke("Syncing inboxes...", "Active");
            _logger.Report("\r\n" + new string('═', 60));
            _logger.Report($"[*] Fast-syncing all accounts in parallel with model load...");

            _emails.Clear();
            PopulateListView();

            try
            {
                // 1. Launch LLM Server in parallel background task
                Task<bool>? serverTask = null;
                if (settings.AutoStartLlamaServer)
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
                        if (this.IsDisposed) return;

                        this.BeginInvoke(new Action(() =>
                        {
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
                    },
                    ct: ct);

                await fetchTask;
                if (serverTask != null) await serverTask;

                int unreadCount = _emails.Count(e => !e.IsRead);
                _logger.Report($"[✓] Parallel sync complete. Loaded {_emails.Count} total email(s) ({unreadCount} unread).");
                StatusUpdated?.Invoke($"Ready • {_emails.Count} emails in inbox ({unreadCount} unread)", "Model Loaded in VRAM");
            }
            catch (OperationCanceledException)
            {
                StatusUpdated?.Invoke("Operation cancelled", "Model Loaded in VRAM");
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

                email.Status = SummaryState.Summarizing;
                string summary = await _llmService.SummarizeEmailAsync(email, settings, ct);
                email.Summary = summary;
                email.Status = SummaryState.Completed;

                _logger.Report($"[✓] Background summary generated for: \"{email.Subject}\"");

                this.BeginInvoke(new Action(() =>
                {
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

            string subjectPrefix = email.IsRead ? "   " : "● ";
            var item = new ListViewItem(subjectPrefix + email.Subject, group);
            item.SubItems.Add(email.AccountName);
            item.SubItems.Add(email.Sender);
            item.SubItems.Add(email.DateString);
            item.Tag = email;

            if (email.IsRead)
            {
                item.ForeColor = Color.FromArgb(130, 135, 145);
                item.Font = new Font("Segoe UI", 8.75F, FontStyle.Regular);
            }
            else
            {
                item.ForeColor = Color.FromArgb(15, 15, 15);
                item.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }

            _lvEmails.Items.Add(item);

            int unreadCount = _emails.Count(e => !e.IsRead);
            _lblInboxHeader.Text = $"Inbox ({_emails.Count} emails, {unreadCount} unread)";
        }

        private void PopulateListView()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(PopulateListView));
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

                string subjectPrefix = email.IsRead ? "   " : "● ";
                var item = new ListViewItem(subjectPrefix + email.Subject, group);
                item.SubItems.Add(email.AccountName);
                item.SubItems.Add(email.Sender);
                item.SubItems.Add(email.DateString);
                item.Tag = email;

                if (email.IsRead)
                {
                    item.ForeColor = Color.FromArgb(130, 135, 145);
                    item.Font = new Font("Segoe UI", 8.75F, FontStyle.Regular);
                }
                else
                {
                    item.ForeColor = Color.FromArgb(15, 15, 15);
                    item.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                }

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

        private async void OnEmailSelected(object? sender, EventArgs e)
        {
            if (_lvEmails.SelectedItems.Count == 0) return;

            var email = _lvEmails.SelectedItems[0].Tag as EmailItem;
            if (email == null) return;

            DisplayEmail(email);

            if (string.IsNullOrWhiteSpace(email.Summary))
            {
                _txtSummary.Text = "✨ Generating AI summary for this email...";
                StatusUpdated?.Invoke($"Summarizing \"{email.Subject}\"...", "In Use (GPU)");

                var settings = _configService.Settings;
                if (settings.AutoStartLlamaServer)
                {
                    await _llamaManager.StartAsync(settings.LlamaModelPath, settings.LlamaServerPort, settings.LlamaGpuLayers, logger: _logger);
                }

                string summary = await _llmService.SummarizeEmailAsync(email, settings);
                email.Summary = summary;
                email.Status = SummaryState.Completed;

                if (_lvEmails.SelectedItems.Count > 0 && _lvEmails.SelectedItems[0].Tag == email)
                {
                    _txtSummary.Text = summary;
                }

                StatusUpdated?.Invoke("Summary ready", "Model Loaded in VRAM");
            }
        }

        private void DisplayEmail(EmailItem email)
        {
            string readTag = email.IsRead ? "[Read]" : "[Unread]";
            _lblEmailSubject.Text = $"Subject: {email.Subject}";
            _lblEmailMeta.Text = $"From: {email.Sender}   •   Date: {email.DateString}   •   Account: {email.AccountName}   •   {readTag}";
            _txtSummary.Text = string.IsNullOrWhiteSpace(email.Summary) ? "✨ Generating AI summary for this email..." : email.Summary;
            _txtEmailBody.Text = email.CleanBody;
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
    }
}
