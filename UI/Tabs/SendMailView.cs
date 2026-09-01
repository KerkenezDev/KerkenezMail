using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    public class SendMailView : UserControl
    {
        public event EventHandler? BackToInboxRequested;
        public event EventHandler? PopOutRequested;
        public event EventHandler? EmailSentSuccessfully;

        private readonly ConfigService _configService;
        private readonly SmtpService _smtpService;

        // UI Controls - Header Bar
        private Button _btnBack = null!;
        private Label _lblTitle = null!;
        private Label _lblThreadBadge = null!;
        private Button _btnPopOut = null!;
        private Button _btnDiscard = null!;
        private Button _btnSend = null!;

        // UI Controls - Recipient Card
        private ComboBox _cboFrom = null!;
        private TextBox _txtTo = null!;
        private TextBox _txtCc = null!;
        private TextBox _txtBcc = null!;
        private TextBox _txtSubject = null!;
        private Label _lblCcToggle = null!;
        private Panel _pnlCcRow = null!;
        private Panel _pnlBccRow = null!;
        private Panel _pnlThreadInfo = null!;
        private Label _lblThreadDetails = null!;

        // UI Controls - Markdown Toolbar & Views
        private Panel _pnlToolbar = null!;
        private Button _btnViewMarkdown = null!;
        private Button _btnViewPlaintext = null!;
        private Button _btnViewHtml = null!;
        private ComboBox _cboFormatMode = null!;

        // Editors
        private TextBox _txtBodyMarkdown = null!;
        private TextBox _txtPlaintextPreview = null!;
        private WebBrowser _wbHtmlPreview = null!;
        private Panel _pnlEditorContainer = null!;

        // UI Controls - Attachments
        private Panel _pnlDropZone = null!;
        private Label _lblDropHint = null!;
        private Button _btnBrowseFiles = null!;
        private FlowLayoutPanel _flpAttachments = null!;
        private Label _lblAttachmentSummary = null!;

        // UI Controls - Status Bar
        private Panel _pnlStatus = null!;
        private Label _lblStatusText = null!;
        private ProgressBar _progBar = null!;

        // State
        private readonly List<AttachmentItem> _attachments = new List<AttachmentItem>();
        private string? _inReplyToId = null;
        private readonly List<string> _referencesList = new List<string>();
        private bool _isReply = false;
        private bool _isSending = false;
        private bool _isDragActive = false;
        private int _currentViewMode = 0; // 0 = Markdown, 1 = Plaintext Preview, 2 = HTML Preview

        public SendMailView(ConfigService configService, SmtpService? smtpService = null)
        {
            _configService = configService;
            _smtpService = smtpService ?? new SmtpService();

            this.DoubleBuffered = true;
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            InitializeComponent();
            RefreshAccountsList();
        }

        private void InitializeComponent()
        {
            float scale = this.DeviceDpi / 96f;

            // ==========================================
            // 1. TOP HEADER ACTION BAR
            // ==========================================
            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(48 * scale),
                BackColor = Color.FromArgb(240, 243, 247),
                Padding = new Padding((int)(12 * scale), (int)(6 * scale), (int)(12 * scale), (int)(6 * scale))
            };
            topBar.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(218, 222, 228), 1);
                e.Graphics.DrawLine(p, 0, topBar.Height - 1, topBar.Width, topBar.Height - 1);
            };

            _btnBack = new Button
            {
                Text = "← Inbox",
                Width = (int)(75 * scale),
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _btnBack.Click += (s, e) => BackToInboxRequested?.Invoke(this, EventArgs.Empty);

            var titlePanel = new Panel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                Padding = new Padding((int)(12 * scale), (int)(8 * scale), 0, 0)
            };

            _lblTitle = new Label
            {
                Text = "✉ Compose New Email",
                AutoSize = true,
                Dock = DockStyle.Left,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 25, 35)
            };

            _lblThreadBadge = new Label
            {
                Text = "🔗 Threaded Reply",
                AutoSize = true,
                Dock = DockStyle.Left,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204),
                BackColor = Color.FromArgb(228, 240, 255),
                Padding = new Padding(4, 2, 4, 2),
                Margin = new Padding(8, 0, 0, 0),
                Visible = false
            };

            titlePanel.Controls.Add(_lblThreadBadge);
            titlePanel.Controls.Add(_lblTitle);

            // Right-aligned actions: Send, Discard, Pop Out
            var rightActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0)
            };

            _btnSend = new Button
            {
                Text = "🚀  Send Email",
                Width = (int)(110 * scale),
                Height = (int)(34 * scale),
                Margin = new Padding((int)(6 * scale), 0, 0, 0),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnSend.Click += async (s, e) => await OnSendClickAsync();

            _btnDiscard = new Button
            {
                Text = "🗑 Discard",
                Width = (int)(80 * scale),
                Height = (int)(34 * scale),
                Margin = new Padding((int)(6 * scale), 0, 0, 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnDiscard.Click += OnDiscardClick;

            _btnPopOut = new Button
            {
                Text = "↗ Pop Out",
                Width = (int)(85 * scale),
                Height = (int)(34 * scale),
                Margin = new Padding((int)(6 * scale), 0, 0, 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnPopOut.Click += (s, e) => PopOutRequested?.Invoke(this, EventArgs.Empty);

            rightActions.Controls.Add(_btnSend);
            rightActions.Controls.Add(_btnDiscard);
            rightActions.Controls.Add(_btnPopOut);

            topBar.Controls.Add(rightActions);
            topBar.Controls.Add(titlePanel);
            topBar.Controls.Add(_btnBack);

            // ==========================================
            // 2. RECIPIENT & ADDRESS CARD
            // ==========================================
            var recipientCard = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White,
                Padding = new Padding((int)(16 * scale), (int)(10 * scale), (int)(16 * scale), (int)(10 * scale))
            };
            recipientCard.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(225, 230, 236), 1);
                e.Graphics.DrawLine(p, 0, recipientCard.Height - 1, recipientCard.Width, recipientCard.Height - 1);
            };

            var fieldsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            fieldsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70 * scale));
            fieldsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Row 0: From
            var lblFrom = new Label { Text = "From:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 6), ForeColor = Color.FromArgb(90, 95, 105) };
            var fromContainer = new Panel { Dock = DockStyle.Fill, Height = (int)(28 * scale), Margin = new Padding(0, 2, 0, 6) };
            
            _cboFrom = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = (int)(320 * scale),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };

            _lblCcToggle = new Label
            {
                Text = "+ Cc / Bcc",
                Dock = DockStyle.Left,
                AutoSize = true,
                Cursor = Cursors.Hand,
                ForeColor = Color.FromArgb(0, 102, 204),
                Padding = new Padding((int)(16 * scale), (int)(5 * scale), 0, 0),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Underline)
            };
            _lblCcToggle.Click += (s, e) => ToggleCcBcc();

            fromContainer.Controls.Add(_lblCcToggle);
            fromContainer.Controls.Add(_cboFrom);

            // Row 1: To
            var lblTo = new Label { Text = "To:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 6), ForeColor = Color.FromArgb(90, 95, 105) };
            _txtTo = new TextBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "recipient@example.com (separate multiple with comma)",
                Margin = new Padding(0, 2, 0, 6),
                Font = new Font("Segoe UI", 9.25F)
            };

            // Row 2: Cc
            _pnlCcRow = new Panel { Dock = DockStyle.Top, Height = (int)(32 * scale), Visible = false };
            var lblCc = new Label { Text = "Cc:", Width = (int)(70 * scale), Dock = DockStyle.Left, ForeColor = Color.FromArgb(90, 95, 105), Padding = new Padding(0, 5, 0, 0) };
            _txtCc = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Cc recipients...", Font = new Font("Segoe UI", 9F) };
            _pnlCcRow.Controls.Add(_txtCc);
            _pnlCcRow.Controls.Add(lblCc);

            // Row 3: Bcc
            _pnlBccRow = new Panel { Dock = DockStyle.Top, Height = (int)(32 * scale), Visible = false };
            var lblBcc = new Label { Text = "Bcc:", Width = (int)(70 * scale), Dock = DockStyle.Left, ForeColor = Color.FromArgb(90, 95, 105), Padding = new Padding(0, 5, 0, 0) };
            _txtBcc = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Bcc recipients...", Font = new Font("Segoe UI", 9F) };
            _pnlBccRow.Controls.Add(_txtBcc);
            _pnlBccRow.Controls.Add(lblBcc);

            // Row 4: Subject
            var lblSubject = new Label { Text = "Subject:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 6), ForeColor = Color.FromArgb(90, 95, 105) };
            _txtSubject = new TextBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "Subject",
                Margin = new Padding(0, 2, 0, 6),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            // Thread info panel
            _pnlThreadInfo = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = Color.FromArgb(245, 248, 252),
                Padding = new Padding(8, 4, 8, 4),
                Margin = new Padding(0, 4, 0, 4),
                Visible = false
            };
            _lblThreadDetails = new Label
            {
                Text = "",
                AutoSize = true,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(60, 90, 130),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Italic)
            };
            _pnlThreadInfo.Controls.Add(_lblThreadDetails);

            fieldsLayout.Controls.Add(lblFrom, 0, 0);
            fieldsLayout.Controls.Add(fromContainer, 1, 0);
            fieldsLayout.Controls.Add(lblTo, 0, 1);
            fieldsLayout.Controls.Add(_txtTo, 1, 1);
            fieldsLayout.Controls.Add(lblSubject, 0, 2);
            fieldsLayout.Controls.Add(_txtSubject, 1, 2);

            recipientCard.Controls.Add(fieldsLayout);
            recipientCard.Controls.Add(_pnlBccRow);
            recipientCard.Controls.Add(_pnlCcRow);
            recipientCard.Controls.Add(_pnlThreadInfo);

            // ==========================================
            // 3. MARKDOWN TOOLBAR & VIEW MODES
            // ==========================================
            _pnlToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(34 * scale),
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding((int)(12 * scale), (int)(2 * scale), (int)(12 * scale), (int)(2 * scale))
            };
            _pnlToolbar.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(225, 230, 236), 1);
                e.Graphics.DrawLine(p, 0, _pnlToolbar.Height - 1, _pnlToolbar.Width, _pnlToolbar.Height - 1);
            };

            var mdButtonsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0)
            };

            mdButtonsFlow.Controls.Add(CreateToolbarButton("B", "Bold (**text**)", (s, e) => WrapSelection("**", "**")));
            mdButtonsFlow.Controls.Add(CreateToolbarButton("I", "Italic (*text*)", (s, e) => WrapSelection("*", "*")));
            mdButtonsFlow.Controls.Add(CreateToolbarButton("H", "Header (### text)", (s, e) => InsertLinePrefix("### ")));
            mdButtonsFlow.Controls.Add(CreateToolbarButton("🔗", "Insert Link ([text](url))", (s, e) => InsertLinkTemplate()));
            mdButtonsFlow.Controls.Add(CreateToolbarButton("• List", "Bullet List (- item)", (s, e) => InsertLinePrefix("- ")));
            mdButtonsFlow.Controls.Add(CreateToolbarButton("1. List", "Numbered List (1. item)", (s, e) => InsertLinePrefix("1. ")));
            mdButtonsFlow.Controls.Add(CreateToolbarButton("❝", "Quote Block (> text)", (s, e) => InsertLinePrefix("> ")));
            mdButtonsFlow.Controls.Add(CreateToolbarButton("</>", "Code Block (```)", (s, e) => WrapSelection("```\n", "\n```")));
            mdButtonsFlow.Controls.Add(CreateToolbarButton("──", "Horizontal Rule (---)", (s, e) => InsertAtCursor("\n---\n")));

            // Right side: View toggles and format mode
            var rightToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0)
            };

            _cboFormatMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = (int)(210 * scale),
                Height = (int)(24 * scale),
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding((int)(8 * scale), 2, 0, 0)
            };
            _cboFormatMode.Items.AddRange(new object[]
            {
                "Markdown (Text + HTML Multipart)",
                "Plaintext Only (Raw RFC Text)"
            });
            _cboFormatMode.SelectedIndex = 0;

            _btnViewHtml = CreateModeToggleButton("🌐 HTML Preview", 2);
            _btnViewPlaintext = CreateModeToggleButton("👁️ Plaintext Preview", 1);
            _btnViewMarkdown = CreateModeToggleButton("✏️ Markdown Edit", 0);

            rightToolbar.Controls.Add(_cboFormatMode);
            rightToolbar.Controls.Add(_btnViewHtml);
            rightToolbar.Controls.Add(_btnViewPlaintext);
            rightToolbar.Controls.Add(_btnViewMarkdown);

            _pnlToolbar.Controls.Add(rightToolbar);
            _pnlToolbar.Controls.Add(mdButtonsFlow);

            // ==========================================
            // 4. ATTACHMENTS & DRAG-AND-DROP PANEL
            // ==========================================
            var attachmentsPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.FromArgb(250, 251, 253),
                Padding = new Padding((int)(12 * scale), (int)(6 * scale), (int)(12 * scale), (int)(6 * scale))
            };
            attachmentsPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(225, 230, 236), 1);
                e.Graphics.DrawLine(p, 0, 0, attachmentsPanel.Width, 0);
            };

            // Drag & drop dropzone
            _pnlDropZone = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)(42 * scale),
                AllowDrop = true,
                BackColor = Color.FromArgb(244, 248, 254),
                Padding = new Padding((int)(10 * scale), (int)(4 * scale), (int)(10 * scale), (int)(4 * scale)),
                Cursor = Cursors.Hand
            };
            _pnlDropZone.Paint += OnDropZonePaint;
            _pnlDropZone.DragEnter += OnDropZoneDragEnter;
            _pnlDropZone.DragOver += OnDropZoneDragOver;
            _pnlDropZone.DragLeave += OnDropZoneDragLeave;
            _pnlDropZone.DragDrop += OnDropZoneDragDrop;
            _pnlDropZone.Click += (s, e) => BrowseAttachments();

            _lblDropHint = new Label
            {
                Text = "📎 Drag & drop attachments here (or click Browse to attach files)",
                AutoSize = true,
                Dock = DockStyle.Left,
                ForeColor = Color.FromArgb(0, 95, 195),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Padding = new Padding(0, (int)(7 * scale), 0, 0)
            };

            _btnBrowseFiles = new Button
            {
                Text = "Browse Files...",
                Dock = DockStyle.Right,
                Width = (int)(100 * scale),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnBrowseFiles.Click += (s, e) => BrowseAttachments();

            _pnlDropZone.Controls.Add(_lblDropHint);
            _pnlDropZone.Controls.Add(_btnBrowseFiles);

            // Flow layout for attached file chips
            _flpAttachments = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Padding = new Padding(0, (int)(4 * scale), 0, (int)(4 * scale)),
                Visible = false
            };

            _lblAttachmentSummary = new Label
            {
                Text = "0 attachments (0 KB)",
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 125, 135),
                Font = new Font("Segoe UI", 8F),
                Padding = new Padding(0, 2, 0, 2),
                Visible = false
            };

            attachmentsPanel.Controls.Add(_lblAttachmentSummary);
            attachmentsPanel.Controls.Add(_flpAttachments);
            attachmentsPanel.Controls.Add(_pnlDropZone);

            // ==========================================
            // 5. STATUS BAR
            // ==========================================
            _pnlStatus = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = (int)(26 * scale),
                BackColor = Color.FromArgb(240, 243, 247),
                Padding = new Padding((int)(12 * scale), (int)(4 * scale), (int)(12 * scale), (int)(4 * scale))
            };
            _pnlStatus.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(220, 224, 230), 1);
                e.Graphics.DrawLine(p, 0, 0, _pnlStatus.Width, 0);
            };

            _lblStatusText = new Label
            {
                Text = "Ready to compose. Markdown formatting supported.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(70, 75, 85),
                Font = new Font("Segoe UI", 8.5F)
            };

            _progBar = new ProgressBar
            {
                Dock = DockStyle.Right,
                Width = (int)(140 * scale),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            _pnlStatus.Controls.Add(_lblStatusText);
            _pnlStatus.Controls.Add(_progBar);

            // ==========================================
            // 6. MAIN EDITOR CONTAINER
            // ==========================================
            _pnlEditorContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            // Markdown editor
            _txtBodyMarkdown = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 10F),
                BorderStyle = BorderStyle.None,
                Padding = new Padding(12),
                AcceptsTab = true,
                PlaceholderText = "Write your email message here using Markdown...\r\n\r\n# Heading\r\n**Bold**, *Italic*, `code`\r\n- Bullet points\r\n> Quoted text\r\n[Link Title](https://example.com)"
            };
            _txtBodyMarkdown.AllowDrop = true;
            _txtBodyMarkdown.DragEnter += OnDropZoneDragEnter;
            _txtBodyMarkdown.DragDrop += OnDropZoneDragDrop;

            // Plaintext preview
            _txtPlaintextPreview = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10F),
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(252, 252, 252),
                Visible = false
            };

            // HTML preview
            _wbHtmlPreview = new WebBrowser
            {
                Dock = DockStyle.Fill,
                Visible = false
            };

            _pnlEditorContainer.Controls.Add(_txtBodyMarkdown);
            _pnlEditorContainer.Controls.Add(_txtPlaintextPreview);
            _pnlEditorContainer.Controls.Add(_wbHtmlPreview);

            // Assemble main layout
            this.Controls.Add(_pnlEditorContainer);
            this.Controls.Add(_pnlToolbar);
            this.Controls.Add(recipientCard);
            this.Controls.Add(topBar);
            this.Controls.Add(attachmentsPanel);
            this.Controls.Add(_pnlStatus);

            UpdateViewMode(0);
        }

        private Button CreateToolbarButton(string text, string tooltip, EventHandler onClick)
        {
            float scale = this.DeviceDpi / 96f;
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = (int)(26 * scale),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(0, 0, (int)(4 * scale), 0)
            };
            btn.Click += onClick;
            var tt = new ToolTip();
            tt.SetToolTip(btn, tooltip);
            return btn;
        }

        private Button CreateModeToggleButton(string text, int modeIndex)
        {
            float scale = this.DeviceDpi / 96f;
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = (int)(26 * scale),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F),
                Margin = new Padding(0, 0, (int)(4 * scale), 0)
            };
            btn.Click += (s, e) => UpdateViewMode(modeIndex);
            return btn;
        }

        private void UpdateViewMode(int mode)
        {
            _currentViewMode = mode;

            _txtBodyMarkdown.Visible = (mode == 0);
            _txtPlaintextPreview.Visible = (mode == 1);
            _wbHtmlPreview.Visible = (mode == 2);

            _btnViewMarkdown.Font = new Font("Segoe UI", 8.5F, mode == 0 ? FontStyle.Bold : FontStyle.Regular);
            _btnViewPlaintext.Font = new Font("Segoe UI", 8.5F, mode == 1 ? FontStyle.Bold : FontStyle.Regular);
            _btnViewHtml.Font = new Font("Segoe UI", 8.5F, mode == 2 ? FontStyle.Bold : FontStyle.Regular);

            if (mode == 1)
            {
                // Generate Plain Text Preview
                string plainText = MarkdownEmailConverter.ConvertToPlainText(_txtBodyMarkdown.Text);
                _txtPlaintextPreview.Text = plainText;
                _lblStatusText.Text = "Previewing Plaintext conversion (RFC text server format).";
            }
            else if (mode == 2)
            {
                // Generate HTML Preview
                string html = MarkdownEmailConverter.ConvertToHtml(_txtBodyMarkdown.Text);
                _wbHtmlPreview.DocumentText = html;
                _lblStatusText.Text = "Previewing Rich HTML email formatting.";
            }
            else
            {
                _lblStatusText.Text = "Editing Markdown. Live preview available via tabs.";
            }
        }

        private void ToggleCcBcc()
        {
            bool show = !_pnlCcRow.Visible;
            _pnlCcRow.Visible = show;
            _pnlBccRow.Visible = show;
            _lblCcToggle.Text = show ? "- Hide Cc/Bcc" : "+ Cc / Bcc";
        }

        public void RefreshAccountsList()
        {
            _cboFrom.Items.Clear();
            var accounts = _configService.GetAccounts().Where(a => a.IsEnabled).ToList();
            if (accounts.Count == 0)
            {
                accounts = _configService.GetAccounts(); // fallback
            }

            foreach (var acc in accounts)
            {
                _cboFrom.Items.Add(acc);
            }

            if (_cboFrom.Items.Count > 0)
            {
                _cboFrom.SelectedIndex = 0;
            }
        }

        public void SetNewEmail(EmailAccount? defaultAccount = null)
        {
            _isReply = false;
            _inReplyToId = null;
            _referencesList.Clear();
            _attachments.Clear();

            _lblTitle.Text = "✉ Compose New Email";
            _lblThreadBadge.Visible = false;
            _pnlThreadInfo.Visible = false;

            _txtTo.Clear();
            _txtCc.Clear();
            _txtBcc.Clear();
            _txtSubject.Clear();
            _txtBodyMarkdown.Clear();

            RefreshAccountsList();
            if (defaultAccount != null)
            {
                for (int i = 0; i < _cboFrom.Items.Count; i++)
                {
                    if (_cboFrom.Items[i] is EmailAccount acc && acc.Email.Equals(defaultAccount.Email, StringComparison.OrdinalIgnoreCase))
                    {
                        _cboFrom.SelectedIndex = i;
                        break;
                    }
                }
            }

            UpdateAttachmentsList();
            UpdateViewMode(0);
            _txtTo.Focus();
        }

        public void SetReplyEmail(EmailItem originalEmail, EmailAccount? senderAccount = null)
        {
            _isReply = true;
            _inReplyToId = originalEmail.MessageId;

            _referencesList.Clear();
            if (originalEmail.References != null && originalEmail.References.Count > 0)
            {
                _referencesList.AddRange(originalEmail.References);
            }
            if (!string.IsNullOrWhiteSpace(originalEmail.MessageId) && !_referencesList.Contains(originalEmail.MessageId))
            {
                _referencesList.Add(originalEmail.MessageId);
            }

            _lblTitle.Text = $"↩ Reply: {originalEmail.Subject}";
            _lblThreadBadge.Visible = true;
            _pnlThreadInfo.Visible = true;
            _lblThreadDetails.Text = $"Threading: In-Reply-To: {originalEmail.MessageId ?? "(none)"} • References: {_referencesList.Count} header(s)";

            // To: sender
            _txtTo.Text = originalEmail.Sender;
            _txtCc.Clear();
            _txtBcc.Clear();

            // Subject: Re: ...
            string subject = originalEmail.Subject.Trim();
            if (!subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase))
            {
                subject = "Re: " + subject;
            }
            _txtSubject.Text = subject;

            // Quote original body
            var quoted = new StringBuilder();
            quoted.AppendLine();
            quoted.AppendLine();
            quoted.AppendLine($"> On {originalEmail.DateString}, {originalEmail.Sender} wrote:");

            string bodyText = !string.IsNullOrWhiteSpace(originalEmail.CleanBody) 
                ? originalEmail.CleanBody 
                : originalEmail.RawBody;

            var bodyLines = bodyText.Replace("\r\n", "\n").Split('\n');
            // Limit quoted lines to prevent excessive bloat
            foreach (var line in bodyLines.Take(120))
            {
                quoted.AppendLine("> " + line);
            }
            if (bodyLines.Length > 120)
            {
                quoted.AppendLine("> [... remaining original message clipped ...]");
            }

            _txtBodyMarkdown.Text = quoted.ToString();
            _txtBodyMarkdown.SelectionStart = 0;
            _txtBodyMarkdown.SelectionLength = 0;

            RefreshAccountsList();

            // Select matching account if provided or matched by email
            string targetEmail = senderAccount?.Email ?? originalEmail.AccountEmail;
            for (int i = 0; i < _cboFrom.Items.Count; i++)
            {
                if (_cboFrom.Items[i] is EmailAccount acc && acc.Email.Equals(targetEmail, StringComparison.OrdinalIgnoreCase))
                {
                    _cboFrom.SelectedIndex = i;
                    break;
                }
            }

            _attachments.Clear();
            UpdateAttachmentsList();
            UpdateViewMode(0);
            _txtBodyMarkdown.Focus();
        }

        #region Formatting Helpers

        private void WrapSelection(string prefix, string suffix)
        {
            if (_currentViewMode != 0) UpdateViewMode(0);

            int start = _txtBodyMarkdown.SelectionStart;
            int length = _txtBodyMarkdown.SelectionLength;
            string selected = _txtBodyMarkdown.SelectedText;

            string replacement = prefix + (string.IsNullOrEmpty(selected) ? "text" : selected) + suffix;
            _txtBodyMarkdown.SelectedText = replacement;

            _txtBodyMarkdown.SelectionStart = start + prefix.Length;
            _txtBodyMarkdown.SelectionLength = string.IsNullOrEmpty(selected) ? 4 : selected.Length;
            _txtBodyMarkdown.Focus();
        }

        private void InsertLinePrefix(string prefix)
        {
            if (_currentViewMode != 0) UpdateViewMode(0);

            int start = _txtBodyMarkdown.SelectionStart;
            _txtBodyMarkdown.SelectedText = "\n" + prefix;
            _txtBodyMarkdown.SelectionStart = start + prefix.Length + 1;
            _txtBodyMarkdown.SelectionLength = 0;
            _txtBodyMarkdown.Focus();
        }

        private void InsertLinkTemplate()
        {
            if (_currentViewMode != 0) UpdateViewMode(0);

            string selected = _txtBodyMarkdown.SelectedText;
            string text = string.IsNullOrEmpty(selected) ? "Link Title" : selected;
            string replacement = $"[{text}](https://)";
            _txtBodyMarkdown.SelectedText = replacement;
            _txtBodyMarkdown.Focus();
        }

        private void InsertAtCursor(string text)
        {
            if (_currentViewMode != 0) UpdateViewMode(0);

            _txtBodyMarkdown.SelectedText = text;
            _txtBodyMarkdown.Focus();
        }

        #endregion

        #region Attachment Handling & Drag & Drop

        private void BrowseAttachments()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select files to attach",
                Multiselect = true,
                Filter = "All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                AddAttachmentFiles(ofd.FileNames);
            }
        }

        private void OnDropZoneDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                _isDragActive = true;
                _pnlDropZone.Invalidate();
            }
        }

        private void OnDropZoneDragOver(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void OnDropZoneDragLeave(object? sender, EventArgs e)
        {
            _isDragActive = false;
            _pnlDropZone.Invalidate();
        }

        private void OnDropZoneDragDrop(object? sender, DragEventArgs e)
        {
            _isDragActive = false;
            _pnlDropZone.Invalidate();

            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    AddAttachmentFiles(files);
                }
            }
        }

        private void OnDropZonePaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(1, 1, _pnlDropZone.Width - 3, _pnlDropZone.Height - 3);

            if (_isDragActive)
            {
                using var activeBg = new SolidBrush(Color.FromArgb(220, 235, 255));
                using var activePen = new Pen(Color.FromArgb(0, 120, 215), 2) { DashStyle = DashStyle.Dash };
                g.FillRectangle(activeBg, bounds);
                g.DrawRectangle(activePen, bounds);
            }
            else
            {
                using var normalPen = new Pen(Color.FromArgb(190, 215, 245), 1) { DashStyle = DashStyle.Dash };
                g.DrawRectangle(normalPen, bounds);
            }
        }

        private void AddAttachmentFiles(string[] filePaths)
        {
            int addedCount = 0;
            foreach (var path in filePaths)
            {
                if (File.Exists(path))
                {
                    if (!_attachments.Any(a => string.Equals(a.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                    {
                        _attachments.Add(AttachmentItem.FromFile(path));
                        addedCount++;
                    }
                }
            }

            if (addedCount > 0)
            {
                UpdateAttachmentsList();
                _lblStatusText.Text = $"Added {addedCount} attachment(s).";
            }
        }

        private void UpdateAttachmentsList()
        {
            _flpAttachments.SuspendLayout();
            _flpAttachments.Controls.Clear();

            float scale = this.DeviceDpi / 96f;

            foreach (var att in _attachments)
            {
                var chip = CreateAttachmentChip(att, scale);
                _flpAttachments.Controls.Add(chip);
            }

            _flpAttachments.ResumeLayout();

            bool hasAttachments = _attachments.Count > 0;
            _flpAttachments.Visible = hasAttachments;
            _lblAttachmentSummary.Visible = hasAttachments;

            if (hasAttachments)
            {
                long totalBytes = _attachments.Sum(a => a.FileSizeBytes);
                string formattedTotal = totalBytes < 1024 * 1024 
                    ? $"{totalBytes / 1024.0:F1} KB" 
                    : $"{totalBytes / (1024.0 * 1024.0):F1} MB";

                _lblAttachmentSummary.Text = $"📎 {_attachments.Count} file(s) attached ({formattedTotal} total • 25 MB provider limit)";
                if (totalBytes > 25 * 1024 * 1024)
                {
                    _lblAttachmentSummary.ForeColor = Color.Red;
                    _lblAttachmentSummary.Text += " ⚠️ Exceeds standard email 25 MB limit";
                }
                else
                {
                    _lblAttachmentSummary.ForeColor = Color.FromArgb(70, 80, 95);
                }
            }
        }

        private Panel CreateAttachmentChip(AttachmentItem att, float scale)
        {
            var chip = new Panel
            {
                Height = (int)(28 * scale),
                AutoSize = true,
                BackColor = Color.FromArgb(240, 244, 250),
                Margin = new Padding(0, 0, (int)(6 * scale), (int)(4 * scale)),
                Padding = new Padding((int)(8 * scale), (int)(3 * scale), (int)(6 * scale), (int)(3 * scale))
            };

            chip.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(205, 218, 235), 1);
                e.Graphics.DrawRectangle(p, 0, 0, chip.Width - 1, chip.Height - 1);
            };

            string icon = GetFileIcon(att.FileName);
            var lblInfo = new Label
            {
                Text = $"{icon} {att.FileName} ({att.FormattedSize})",
                AutoSize = true,
                Dock = DockStyle.Left,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(30, 40, 55),
                Padding = new Padding(0, (int)(2 * scale), (int)(6 * scale), 0)
            };

            var btnRemove = new Button
            {
                Text = "✕",
                Width = (int)(20 * scale),
                Height = (int)(20 * scale),
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 120, 120)
            };
            btnRemove.FlatAppearance.BorderSize = 0;
            btnRemove.MouseEnter += (s, e) => { btnRemove.ForeColor = Color.Red; };
            btnRemove.MouseLeave += (s, e) => { btnRemove.ForeColor = Color.FromArgb(120, 120, 120); };
            btnRemove.Click += (s, e) =>
            {
                _attachments.Remove(att);
                UpdateAttachmentsList();
            };

            chip.Controls.Add(lblInfo);
            chip.Controls.Add(btnRemove);
            return chip;
        }

        private static string GetFileIcon(string filename)
        {
            string ext = Path.GetExtension(filename).ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "📄",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "🖼️",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
                ".txt" or ".md" or ".log" => "📝",
                ".docx" or ".doc" => "📘",
                ".xlsx" or ".xls" or ".csv" => "📊",
                ".pptx" or ".ppt" => "📙",
                _ => "📎"
            };
        }

        #endregion

        #region Sending & Discard

        private async Task OnSendClickAsync()
        {
            if (_isSending) return;

            string to = _txtTo.Text.Trim();
            if (string.IsNullOrWhiteSpace(to))
            {
                MessageBox.Show(this, "Please enter at least one recipient in the 'To' field.", "Missing Recipient", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtTo.Focus();
                return;
            }

            if (_cboFrom.SelectedItem is not EmailAccount account)
            {
                MessageBox.Show(this, "Please select an account to send from.", "Missing Account", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string subject = _txtSubject.Text.Trim();
            if (string.IsNullOrWhiteSpace(subject))
            {
                var res = MessageBox.Show(this, "Send this message without a subject?", "No Subject", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res != DialogResult.Yes)
                {
                    _txtSubject.Focus();
                    return;
                }
            }

            // Prepare draft
            var draft = new SendMailDraft
            {
                FromAccount = account,
                To = to,
                Cc = _txtCc.Text.Trim(),
                Bcc = _txtBcc.Text.Trim(),
                Subject = subject,
                BodyMarkdown = _txtBodyMarkdown.Text,
                InReplyTo = _inReplyToId,
                References = new List<string>(_referencesList),
                Attachments = new List<AttachmentItem>(_attachments),
                IsReply = _isReply,
                SendAsPlaintextOnly = _cboFormatMode.SelectedIndex == 1
            };

            // Set UI sending state
            SetSendingState(true);

            var progress = new Progress<string>(msg =>
            {
                if (this.InvokeRequired) this.BeginInvoke(() => _lblStatusText.Text = msg);
                else _lblStatusText.Text = msg;
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            try
            {
                bool saveSentToImap = _configService.Settings.SaveSentEmailsToImap;
                var (success, msg) = await _smtpService.SendEmailAsync(draft, saveSentToImap, progress, cts.Token);

                SetSendingState(false);

                if (success)
                {
                    _lblStatusText.Text = "✓ " + msg;
                    MessageBox.Show(this, msg, "Email Sent", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    EmailSentSuccessfully?.Invoke(this, EventArgs.Empty);
                    BackToInboxRequested?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    _lblStatusText.Text = "✗ " + msg;
                    MessageBox.Show(this, msg, "Failed to Send Email", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                SetSendingState(false);
                _lblStatusText.Text = "✗ Error: " + ex.Message;
                MessageBox.Show(this, "Error sending email: " + ex.Message, "Send Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetSendingState(bool sending)
        {
            _isSending = sending;
            _btnSend.Enabled = !sending;
            _btnDiscard.Enabled = !sending;
            _btnBack.Enabled = !sending;
            _progBar.Visible = sending;
            if (sending)
            {
                _lblStatusText.Text = "Sending email via SMTP...";
            }
        }

        private void OnDiscardClick(object? sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_txtTo.Text) || !string.IsNullOrWhiteSpace(_txtBodyMarkdown.Text) || _attachments.Count > 0)
            {
                var res = MessageBox.Show(this, "Are you sure you want to discard this draft?", "Discard Draft", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res != DialogResult.Yes) return;
            }

            SetNewEmail();
            BackToInboxRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
