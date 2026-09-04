using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using KerkenezMail.Languages;
using KerkenezMail.Models;
using KerkenezMail.Services;

namespace KerkenezMail.UI.Tabs
{
    public class AccountsView : UserControl
    {
        private readonly ConfigService _configService;
        private readonly ImapService _imapService;
        private readonly IProgress<string> _logger;

        private FlowLayoutPanel _pnlCards = null!;
        private Label _lblTitle = null!;
        private Button _btnAddAccount = null!;
        private Button _btnTestAll = null!;
        private Button _btnBottomAdd = null!;
        private Label _lblEmpty = null!;

        public event Action? AccountsChanged;

        public AccountsView(ConfigService configService, ImapService imapService, IProgress<string> logger)
        {
            _configService = configService;
            _imapService = imapService;
            _logger = logger;

            InitializeComponent();
            LoadAccounts();
            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLocalization();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            // Top Action Toolbar
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(14, 10, 14, 10),
                BackColor = Color.White
            };

            topPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(225, 228, 232));
                e.Graphics.DrawLine(p, 0, topPanel.Height - 1, topPanel.Width, topPanel.Height - 1);
            };

            _lblTitle = new Label
            {
                Text = Lang.T(StringKeys.AccountsTitle),
                Dock = DockStyle.Left,
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 30, 30),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 4, 0, 0)
            };

            var rightActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0)
            };

            _btnAddAccount = new Button
            {
                Text = "➕ " + Lang.T(StringKeys.AccountsBtnAdd),
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 5, 12, 5),
                Margin = new Padding(0, 0, 8, 0),
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnAddAccount.Click += OnAddAccountClick;

            _btnTestAll = new Button
            {
                Text = "⚡ " + Lang.T(StringKeys.AccountsBtnTest),
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 5, 12, 5),
                Margin = new Padding(0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnTestAll.Click += async (s, e) => await TestAllAccountsAsync();

            rightActions.Controls.Add(_btnAddAccount);
            rightActions.Controls.Add(_btnTestAll);

            topPanel.Controls.Add(_lblTitle);
            topPanel.Controls.Add(rightActions);

            // Card container
            _pnlCards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(14, 12, 14, 14),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };

            _lblEmpty = new Label
            {
                Text = Lang.T(StringKeys.AccountsEmptyDesc),
                AutoSize = false,
                Size = new Size(500, 80),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(120, 120, 120),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                Visible = false
            };
            _pnlCards.Controls.Add(_lblEmpty);

            _btnBottomAdd = new Button
            {
                Text = "➕ " + Lang.T(StringKeys.AccountsBtnAdd),
                UseMnemonic = false,
                Width = 500,
                Height = 38,
                Margin = new Padding(0, 8, 0, 20),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 102, 204),
                BackColor = Color.FromArgb(240, 246, 255),
                Cursor = Cursors.Hand
            };
            _btnBottomAdd.FlatAppearance.BorderColor = Color.FromArgb(180, 215, 250);
            _btnBottomAdd.Click += OnAddAccountClick;

            this.Controls.Add(_pnlCards);
            this.Controls.Add(topPanel);

            this.Resize += (s, e) => RefreshCardWidths();
        }

        private void RefreshCardWidths()
        {
            int targetWidth = Math.Max(460, _pnlCards.ClientSize.Width - 32);
            foreach (Control c in _pnlCards.Controls)
            {
                if (c is Panel p)
                {
                    p.Width = targetWidth;
                }
                else if (c == _btnBottomAdd)
                {
                    _btnBottomAdd.Width = targetWidth;
                }
            }
        }

        public void LoadAccounts()
        {
            _pnlCards.SuspendLayout();
            _pnlCards.Controls.Clear();

            var accounts = _configService.GetAccounts();
            if (accounts.Count == 0)
            {
                _lblEmpty.Visible = true;
                _pnlCards.Controls.Add(_lblEmpty);
            }
            else
            {
                _lblEmpty.Visible = false;
                int targetWidth = Math.Max(460, _pnlCards.ClientSize.Width - 32);
                foreach (var acc in accounts)
                {
                    var card = CreateAccountCard(acc, targetWidth);
                    _pnlCards.Controls.Add(card);
                }
            }

            // Always provide prominent bottom Add button
            int addBtnWidth = Math.Max(460, _pnlCards.ClientSize.Width - 32);
            _btnBottomAdd.Width = addBtnWidth;
            _pnlCards.Controls.Add(_btnBottomAdd);

            _pnlCards.ResumeLayout();
        }

        private static string FormatConnectionStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status) || status.Equals("Untested", StringComparison.OrdinalIgnoreCase))
            {
                return Lang.T(StringKeys.AccountsStatusUntested);
            }
            if (status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase))
            {
                var match = System.Text.RegularExpressions.Regex.Match(status, @"\(([0-9]+)\s+unread\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int unread))
                {
                    return Lang.Format(StringKeys.AccountsStatusConnectedUnread, unread);
                }
                return Lang.T(StringKeys.AccountsStatusConnected);
            }
            if (status.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
            {
                return Lang.T(StringKeys.AccountsStatusFailed);
            }
            return status;
        }

        private Panel CreateAccountCard(EmailAccount account, int width)
        {
            var card = new Panel
            {
                Width = width,
                Height = 84,
                Margin = new Padding(0, 0, 0, 10),
                BackColor = Color.White,
                Padding = new Padding(12, 6, 12, 6)
            };

            card.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(220, 224, 230), 1);
                e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            // Left: Enable checkbox + Name & Email
            var chkEnabled = new CheckBox
            {
                Checked = account.IsEnabled,
                Text = "",
                AutoSize = true,
                Left = 10,
                Top = 12,
                Cursor = Cursors.Hand
            };
            chkEnabled.CheckedChanged += (s, e) =>
            {
                account.IsEnabled = chkEnabled.Checked;
                var accounts = _configService.GetAccounts();
                var existing = accounts.FirstOrDefault(a => a.Id == account.Id);
                if (existing != null)
                {
                    existing.IsEnabled = account.IsEnabled;
                }
                _configService.SaveAccounts(accounts);
                AccountsChanged?.Invoke();
            };

            var lblName = new Label
            {
                Text = account.Name,
                Font = new Font("Segoe UI", 9.75F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 20, 20),
                AutoSize = true,
                Left = 36,
                Top = 8
            };

            string providerBadge = account.IsOutlookOAuth ? "  •  🔐 OAuth 2.0" : "";
            var lblEmail = new Label
            {
                Text = $"📧 {account.Email}  •  🌐 {account.Host}:{account.Port} ({(account.UseSsl ? "SSL" : "Plain")}){providerBadge}",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(90, 90, 90),
                AutoSize = true,
                Left = 36,
                Top = 32
            };

            var lblStatus = new Label
            {
                Text = $"{Lang.T(StringKeys.AccountsColStatus)}: {FormatConnectionStatus(account.ConnectionStatus)}",
                Font = new Font("Segoe UI", 8.25F, FontStyle.Italic),
                ForeColor = account.ConnectionStatus.StartsWith("Connected") ? Color.DarkGreen :
                            account.ConnectionStatus.StartsWith("Failed") ? Color.Red : Color.FromArgb(120, 120, 120),
                AutoSize = true,
                Left = 36,
                Top = 54
            };

            // Right: Action buttons
            var pnlActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 14, 0, 0)
            };

            var btnTest = new Button
            {
                Text = "⚡ " + Lang.T(StringKeys.AccountsBtnTest),
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0, 0, 6, 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            btnTest.Click += async (s, e) =>
            {
                btnTest.Enabled = false;
                lblStatus.ForeColor = Color.DarkOrange;
                lblStatus.Text = $"{Lang.T(StringKeys.AccountsColStatus)}: {Lang.T(StringKeys.AddAccTesting)}";

                var (success, msg, unread) = await _imapService.TestConnectionAsync(account);
                account.ConnectionStatus = success ? $"Connected ({unread} unread)" : "Failed";
                account.ConnectionError = success ? null : msg;

                lblStatus.ForeColor = success ? Color.DarkGreen : Color.Red;
                lblStatus.Text = $"{Lang.T(StringKeys.AccountsColStatus)}: {FormatConnectionStatus(account.ConnectionStatus)}";
                btnTest.Enabled = true;

                if (!success)
                {
                    MessageBox.Show(msg, Lang.T(StringKeys.AccountsStatusError), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var btnEdit = new Button
            {
                Text = "✏️ " + Lang.T(StringKeys.AccountsBtnEdit),
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0, 0, 6, 0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            btnEdit.Click += (s, e) =>
            {
                using var dlg = new AddAccountDialog(_imapService, account);
                if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
                {
                    var updated = dlg.ResultAccount;
                    account.Name = updated.Name;
                    account.Email = updated.Email;
                    account.AppPassword = updated.AppPassword;
                    account.Host = updated.Host;
                    account.Port = updated.Port;
                    account.UseSsl = updated.UseSsl;
                    account.SmtpHost = updated.SmtpHost;
                    account.SmtpPort = updated.SmtpPort;
                    account.SmtpUseSsl = updated.SmtpUseSsl;
                    account.Provider = updated.Provider;
                    account.EncryptedAccessToken = updated.EncryptedAccessToken;
                    account.EncryptedRefreshToken = updated.EncryptedRefreshToken;
                    account.AccessTokenExpiresUtc = updated.AccessTokenExpiresUtc;
                    account.LastRefreshedUtc = updated.LastRefreshedUtc;

                    var accounts = _configService.GetAccounts();
                    var idx = accounts.FindIndex(a => a.Id == account.Id);
                    if (idx >= 0) accounts[idx] = account;
                    else accounts.Add(account);

                    _configService.SaveAccounts(accounts);
                    LoadAccounts();
                    AccountsChanged?.Invoke();
                }
            };

            var btnDelete = new Button
            {
                Text = "🗑️",
                UseMnemonic = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8, 4, 8, 4),
                Margin = new Padding(0),
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            btnDelete.Click += (s, e) =>
            {
                string msg = Lang.Format(StringKeys.AccountsDeleteConfirm, account.Name, account.Email);
                if (MessageBox.Show(msg, Lang.T(StringKeys.CommonDelete), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    var accounts = _configService.GetAccounts();
                    accounts.RemoveAll(a => a.Id == account.Id);
                    _configService.SaveAccounts(accounts);
                    LoadAccounts();
                    AccountsChanged?.Invoke();
                }
            };

            var actionTips = new ToolTip();
            actionTips.SetToolTip(btnTest, Lang.T(StringKeys.AccountsBtnTest));
            actionTips.SetToolTip(btnEdit, Lang.T(StringKeys.AccountsBtnEdit));
            actionTips.SetToolTip(btnDelete, Lang.T(StringKeys.AccountsBtnDelete));

            pnlActions.Controls.Add(btnTest);
            pnlActions.Controls.Add(btnEdit);
            pnlActions.Controls.Add(btnDelete);

            card.Controls.Add(chkEnabled);
            card.Controls.Add(lblName);
            card.Controls.Add(lblEmail);
            card.Controls.Add(lblStatus);
            card.Controls.Add(pnlActions);

            return card;
        }

        private void OnAddAccountClick(object? sender, EventArgs e)
        {
            using var dlg = new AddAccountDialog(_imapService);
            if (dlg.ShowDialog(this.FindForm()) == DialogResult.OK)
            {
                var accounts = _configService.GetAccounts();
                accounts.Add(dlg.ResultAccount);
                _configService.SaveAccounts(accounts);
                LoadAccounts();
                AccountsChanged?.Invoke();
                _logger.Report($"[+] Added new account: {dlg.ResultAccount.Name} ({dlg.ResultAccount.Email})");
            }
        }

        public async Task TestAllAccountsAsync()
        {
            _btnTestAll.Enabled = false;
            _logger.Report("\r\n[*] Testing connections for all configured accounts...");

            var accounts = _configService.GetAccounts();
            foreach (var acc in accounts)
            {
                var (success, msg, unread) = await _imapService.TestConnectionAsync(acc);
                acc.ConnectionStatus = success ? $"Connected ({unread} unread)" : "Failed";
                acc.ConnectionError = success ? null : msg;

                if (success)
                {
                    _logger.Report($"[✓] {acc.Name} ({acc.Email}): Connected successfully. {unread} unread email(s).");
                }
                else
                {
                    _logger.Report($"[!] {acc.Name} ({acc.Email}): {msg}");
                }
            }

            LoadAccounts();
            _btnTestAll.Enabled = true;
        }

        public void ApplyLocalization()
        {
            if (this.IsDisposed) return;
            if (_lblTitle != null) _lblTitle.Text = Lang.T(StringKeys.AccountsTitle);
            if (_btnAddAccount != null) _btnAddAccount.Text = "➕ " + Lang.T(StringKeys.AccountsBtnAdd);
            if (_btnTestAll != null) _btnTestAll.Text = "⚡ " + Lang.T(StringKeys.AccountsBtnTest);
            if (_btnBottomAdd != null) _btnBottomAdd.Text = "➕ " + Lang.T(StringKeys.AccountsBtnAdd);
            if (_lblEmpty != null) _lblEmpty.Text = Lang.T(StringKeys.AccountsEmptyDesc);
            LoadAccounts();
        }
    }
}
