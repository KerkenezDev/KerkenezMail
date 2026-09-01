using System;
using System.Drawing;
using System.Windows.Forms;
using EmailSummarizer.Models;
using EmailSummarizer.Services;

namespace EmailSummarizer.UI
{
    public class AddAccountDialog : Form
    {
        private readonly ImapService _imapService;
        private readonly EmailAccount _account;
        private readonly bool _isEditMode;

        private TextBox _txtName = null!;
        private ComboBox _cboProvider = null!;
        private TextBox _txtEmail = null!;
        private TextBox _txtPassword = null!;
        private Button _btnShowPassword = null!;
        private TextBox _txtHost = null!;
        private NumericUpDown _numPort = null!;
        private CheckBox _chkUseSsl = null!;
        private TextBox _txtSmtpHost = null!;
        private NumericUpDown _numSmtpPort = null!;
        private CheckBox _chkSmtpUseSsl = null!;
        private Button _btnTestConnection = null!;
        private Label _lblTestResult = null!;
        private Button _btnSave = null!;
        private Button _btnCancel = null!;

        public EmailAccount ResultAccount => _account;

        public AddAccountDialog(ImapService imapService, EmailAccount? existingAccount = null)
        {
            _imapService = imapService;
            _isEditMode = existingAccount != null;
            _account = existingAccount != null
                ? new EmailAccount
                {
                    Id = existingAccount.Id,
                    Name = existingAccount.Name,
                    Email = existingAccount.Email,
                    AppPassword = existingAccount.AppPassword,
                    Host = existingAccount.Host,
                    Port = existingAccount.Port,
                    UseSsl = existingAccount.UseSsl,
                    IsEnabled = existingAccount.IsEnabled
                }
                : new EmailAccount();

            InitializeComponent();
            PopulateData();
        }

        private void InitializeComponent()
        {
            this.Text = _isEditMode ? "Edit Email Account" : "Add Email Account";
            try
            {
                using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("EmailSummarizer.app.ico");
                if (stream != null) this.Icon = new Icon(stream);
                else if (File.Exists("app.ico")) this.Icon = new Icon("app.ico");
            }
            catch { }

            this.Size = new Size(540, 600);
            this.MinimumSize = new Size(460, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(248, 249, 250)
            };

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Padding = new Padding(24, 16, 24, 16),
                ColumnCount = 2,
                RowCount = 12,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // 1. Account Name
            var lblName = new Label { Text = "Account Label:", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 8, 0, 8) };
            _txtName = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 10), Font = new Font("Segoe UI", 9.5F) };

            // 2. Provider Preset
            var lblProvider = new Label { Text = "Provider:", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 8, 0, 8) };
            _cboProvider = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 4, 0, 10),
                Font = new Font("Segoe UI", 9.5F)
            };
            _cboProvider.Items.AddRange(new object[]
            {
                "Gmail (imap.gmail.com:993)",
                "Yahoo Mail (imap.mail.yahoo.com:993)",
                "iCloud Mail (imap.mail.me.com:993)",
                "Custom IMAP Server"
            });
            _cboProvider.SelectedIndex = 0;
            _cboProvider.SelectedIndexChanged += OnProviderChanged;

            // 3. Email Address
            var lblEmail = new Label { Text = "Email Address:", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 8, 0, 8) };
            _txtEmail = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 10), Font = new Font("Segoe UI", 9.5F) };
            _txtEmail.TextChanged += OnEmailTextChanged;

            // 4. App Password
            var lblPassword = new Label { Text = "App Password:", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 8, 0, 8) };
            
            var pwdPanel = new Panel { Dock = DockStyle.Fill, Height = 32, Margin = new Padding(0, 4, 0, 4) };
            _txtPassword = new TextBox
            {
                UseSystemPasswordChar = true,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F)
            };
            _btnShowPassword = new Button
            {
                Text = "👁",
                Width = 36,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F)
            };
            _btnShowPassword.FlatAppearance.BorderColor = Color.LightGray;
            _btnShowPassword.Click += (s, e) =>
            {
                _txtPassword.UseSystemPasswordChar = !_txtPassword.UseSystemPasswordChar;
            };
            pwdPanel.Controls.Add(_txtPassword);
            pwdPanel.Controls.Add(_btnShowPassword);

            // 5. Help text / note
            var lblHelpNote = new Label
            {
                Text = "💡 For Gmail: Use a 16-character Google App Password (myaccount.google.com/apppasswords). Standard Google passwords will not work with 2FA enabled.",
                ForeColor = Color.FromArgb(80, 80, 80),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Italic),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 12)
            };

            // 6. Host & Port
            var lblHost = new Label { Text = "IMAP Host & Port:", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 8, 0, 8) };
            var hostPortPanel = new Panel { Dock = DockStyle.Fill, Height = 32, Margin = new Padding(0, 4, 0, 10) };
            _txtHost = new TextBox { Text = "imap.gmail.com", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F) };
            _numPort = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 65535,
                Value = 993,
                Width = 70,
                Dock = DockStyle.Right,
                Font = new Font("Segoe UI", 9.5F)
            };
            var lblColon = new Label { Text = ":", Width = 15, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Right };
            hostPortPanel.Controls.Add(_txtHost);
            hostPortPanel.Controls.Add(lblColon);
            hostPortPanel.Controls.Add(_numPort);

            // 7. SSL Checkbox
            _chkUseSsl = new CheckBox
            {
                Text = "Use SSL / TLS for IMAP",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 8)
            };

            // 7b. SMTP Host & Port
            var lblSmtpHost = new Label { Text = "SMTP Host & Port:", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, 8, 0, 8) };
            var smtpHostPortPanel = new Panel { Dock = DockStyle.Fill, Height = 32, Margin = new Padding(0, 4, 0, 8) };
            _txtSmtpHost = new TextBox { Text = "smtp.gmail.com", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F) };
            _numSmtpPort = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 65535,
                Value = 587,
                Width = 70,
                Dock = DockStyle.Right,
                Font = new Font("Segoe UI", 9.5F)
            };
            var lblSmtpColon = new Label { Text = ":", Width = 15, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Right };
            smtpHostPortPanel.Controls.Add(_txtSmtpHost);
            smtpHostPortPanel.Controls.Add(lblSmtpColon);
            smtpHostPortPanel.Controls.Add(_numSmtpPort);

            // 7c. SMTP SSL Checkbox
            _chkSmtpUseSsl = new CheckBox
            {
                Text = "Use SSL on Connect for SMTP (Uncheck for STARTTLS on port 587)",
                Checked = false,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 12)
            };

            // 8. Test Connection Button & Result
            var testPanel = new Panel { Dock = DockStyle.Fill, Height = 36, Margin = new Padding(0, 4, 0, 8) };
            _btnTestConnection = new Button
            {
                Text = "⚡ Test Connection",
                Width = 140,
                Height = 32,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.System,
                Cursor = Cursors.Hand
            };
            _btnTestConnection.Click += OnTestConnectionClick;

            _lblTestResult = new Label
            {
                Text = "",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Font = new Font("Segoe UI", 8.5F)
            };
            testPanel.Controls.Add(_lblTestResult);
            testPanel.Controls.Add(_btnTestConnection);

            // Bottom Buttons
            var bottomButtonsPanel = new Panel 
            { 
                Dock = DockStyle.Bottom, 
                Height = 56, 
                Padding = new Padding(0, 12, 24, 12),
                BackColor = Color.FromArgb(242, 244, 247)
            };
            bottomButtonsPanel.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(222, 226, 230), 1);
                e.Graphics.DrawLine(p, 0, 0, bottomButtonsPanel.Width, 0);
            };

            _btnCancel = new Button
            {
                Text = "Cancel",
                Width = 90,
                Height = 32,
                Dock = DockStyle.Right,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.System
            };
            _btnSave = new Button
            {
                Text = _isEditMode ? "Save Changes" : "Add Account",
                Width = 120,
                Height = 32,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnSave.Click += OnSaveClick;

            var spacer = new Panel { Width = 10, Dock = DockStyle.Right };
            bottomButtonsPanel.Controls.Add(_btnSave);
            bottomButtonsPanel.Controls.Add(spacer);
            bottomButtonsPanel.Controls.Add(_btnCancel);

            // Add rows
            mainPanel.Controls.Add(lblName, 0, 0);
            mainPanel.Controls.Add(_txtName, 1, 0);

            mainPanel.Controls.Add(lblProvider, 0, 1);
            mainPanel.Controls.Add(_cboProvider, 1, 1);

            mainPanel.Controls.Add(lblEmail, 0, 2);
            mainPanel.Controls.Add(_txtEmail, 1, 2);

            mainPanel.Controls.Add(lblPassword, 0, 3);
            mainPanel.Controls.Add(pwdPanel, 1, 3);

            mainPanel.Controls.Add(new Label(), 0, 4);
            mainPanel.Controls.Add(lblHelpNote, 1, 4);

            mainPanel.Controls.Add(lblHost, 0, 5);
            mainPanel.Controls.Add(hostPortPanel, 1, 5);

            mainPanel.Controls.Add(new Label(), 0, 6);
            mainPanel.Controls.Add(_chkUseSsl, 1, 6);

            mainPanel.Controls.Add(lblSmtpHost, 0, 7);
            mainPanel.Controls.Add(smtpHostPortPanel, 1, 7);

            mainPanel.Controls.Add(new Label(), 0, 8);
            mainPanel.Controls.Add(_chkSmtpUseSsl, 1, 8);

            mainPanel.Controls.Add(new Label(), 0, 9);
            mainPanel.Controls.Add(testPanel, 1, 9);

            scrollPanel.Controls.Add(mainPanel);
            this.Controls.Add(scrollPanel);
            this.Controls.Add(bottomButtonsPanel);

            this.AcceptButton = _btnSave;
            this.CancelButton = _btnCancel;
        }

        private void PopulateData()
        {
            if (_isEditMode)
            {
                _txtName.Text = _account.Name;
                _txtEmail.Text = _account.Email;
                _txtPassword.Text = _account.AppPassword;
                _txtHost.Text = _account.Host;
                _numPort.Value = _account.Port;
                _chkUseSsl.Checked = _account.UseSsl;
                _txtSmtpHost.Text = _account.GetEffectiveSmtpHost();
                _numSmtpPort.Value = _account.GetEffectiveSmtpPort();
                _chkSmtpUseSsl.Checked = _account.SmtpUseSsl;

                // Match provider
                if (_account.Host.Contains("gmail")) _cboProvider.SelectedIndex = 0;
                else if (_account.Host.Contains("yahoo")) _cboProvider.SelectedIndex = 1;
                else if (_account.Host.Contains("mail.me.com")) _cboProvider.SelectedIndex = 2;
                else _cboProvider.SelectedIndex = 3;
            }
            else
            {
                _txtName.Text = "My Gmail";
                _txtHost.Text = "imap.gmail.com";
                _numPort.Value = 993;
                _chkUseSsl.Checked = true;
                _txtSmtpHost.Text = "smtp.gmail.com";
                _numSmtpPort.Value = 587;
                _chkSmtpUseSsl.Checked = false;
            }
        }

        private void OnProviderChanged(object? sender, EventArgs e)
        {
            switch (_cboProvider.SelectedIndex)
            {
                case 0: // Gmail
                    _txtHost.Text = "imap.gmail.com";
                    _numPort.Value = 993;
                    _chkUseSsl.Checked = true;
                    _txtHost.Enabled = false;
                    _numPort.Enabled = false;
                    _txtSmtpHost.Text = "smtp.gmail.com";
                    _numSmtpPort.Value = 587;
                    _chkSmtpUseSsl.Checked = false;
                    _txtSmtpHost.Enabled = false;
                    _numSmtpPort.Enabled = false;
                    _chkSmtpUseSsl.Enabled = false;
                    break;
                case 1: // Yahoo
                    _txtHost.Text = "imap.mail.yahoo.com";
                    _numPort.Value = 993;
                    _chkUseSsl.Checked = true;
                    _txtHost.Enabled = false;
                    _numPort.Enabled = false;
                    _txtSmtpHost.Text = "smtp.mail.yahoo.com";
                    _numSmtpPort.Value = 465;
                    _chkSmtpUseSsl.Checked = true;
                    _txtSmtpHost.Enabled = false;
                    _numSmtpPort.Enabled = false;
                    _chkSmtpUseSsl.Enabled = false;
                    break;
                case 2: // iCloud
                    _txtHost.Text = "imap.mail.me.com";
                    _numPort.Value = 993;
                    _chkUseSsl.Checked = true;
                    _txtHost.Enabled = false;
                    _numPort.Enabled = false;
                    _txtSmtpHost.Text = "smtp.mail.me.com";
                    _numSmtpPort.Value = 587;
                    _chkSmtpUseSsl.Checked = false;
                    _txtSmtpHost.Enabled = false;
                    _numSmtpPort.Enabled = false;
                    _chkSmtpUseSsl.Enabled = false;
                    break;
                default: // Custom
                    _txtHost.Enabled = true;
                    _numPort.Enabled = true;
                    _txtSmtpHost.Enabled = true;
                    _numSmtpPort.Enabled = true;
                    _chkSmtpUseSsl.Enabled = true;
                    break;
            }
        }

        private void OnEmailTextChanged(object? sender, EventArgs e)
        {
            string email = _txtEmail.Text.Trim();
            if (!_isEditMode && email.Contains("@"))
            {
                if (email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
                {
                    _cboProvider.SelectedIndex = 0;
                }
                else if (email.EndsWith("@yahoo.com", StringComparison.OrdinalIgnoreCase))
                {
                    _cboProvider.SelectedIndex = 1;
                }
                else if (email.EndsWith("@icloud.com", StringComparison.OrdinalIgnoreCase) ||
                         email.EndsWith("@me.com", StringComparison.OrdinalIgnoreCase))
                {
                    _cboProvider.SelectedIndex = 2;
                }
            }
        }

        private async void OnTestConnectionClick(object? sender, EventArgs e)
        {
            string email = _txtEmail.Text.Trim();
            string password = _txtPassword.Text.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                _lblTestResult.ForeColor = Color.Red;
                _lblTestResult.Text = "Please enter an email address.";
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                _lblTestResult.ForeColor = Color.Red;
                _lblTestResult.Text = "Please enter an App Password.";
                return;
            }

            _btnTestConnection.Enabled = false;
            _lblTestResult.ForeColor = Color.DarkOrange;
            _lblTestResult.Text = "Testing connection...";

            var tempAccount = new EmailAccount
            {
                Name = _txtName.Text.Trim(),
                Email = email,
                AppPassword = password,
                Host = _txtHost.Text.Trim(),
                Port = (int)_numPort.Value,
                UseSsl = _chkUseSsl.Checked,
                SmtpHost = _txtSmtpHost.Text.Trim(),
                SmtpPort = (int)_numSmtpPort.Value,
                SmtpUseSsl = _chkSmtpUseSsl.Checked
            };

            var (imapOk, imapMsg, unread) = await _imapService.TestConnectionAsync(tempAccount);

            if (!imapOk)
            {
                _btnTestConnection.Enabled = true;
                _lblTestResult.ForeColor = Color.Red;
                _lblTestResult.Text = $"✗ IMAP: {imapMsg}";
                return;
            }

            var smtpService = new SmtpService();
            var (smtpOk, smtpMsg) = await smtpService.TestSmtpConnectionAsync(tempAccount);

            _btnTestConnection.Enabled = true;
            if (smtpOk)
            {
                _lblTestResult.ForeColor = Color.DarkGreen;
                _lblTestResult.Text = $"✓ IMAP & SMTP Verified! ({unread} unread)";
            }
            else
            {
                _lblTestResult.ForeColor = Color.DarkOrange;
                _lblTestResult.Text = $"✓ IMAP OK, but ✗ SMTP: {smtpMsg}";
            }
        }

        private void OnSaveClick(object? sender, EventArgs e)
        {
            string name = _txtName.Text.Trim();
            string email = _txtEmail.Text.Trim();
            string password = _txtPassword.Text.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show("Please enter an email address.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtEmail.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please enter an App Password.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtPassword.Focus();
                return;
            }

            _account.Name = string.IsNullOrWhiteSpace(name) ? email : name;
            _account.Email = email;
            _account.AppPassword = password;
            _account.Host = _txtHost.Text.Trim();
            _account.Port = (int)_numPort.Value;
            _account.UseSsl = _chkUseSsl.Checked;
            _account.SmtpHost = _txtSmtpHost.Text.Trim();
            _account.SmtpPort = (int)_numSmtpPort.Value;
            _account.SmtpUseSsl = _chkSmtpUseSsl.Checked;
            _account.IsEnabled = true;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
