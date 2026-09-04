using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace KerkenezMail.UI
{
    public class RebrandingNoticeDialog : Form
    {
        private const string WinGetCommand = "winget install ismlEraslan.KerkenezMail";
        private const string GitHubReleasesUrl = "https://github.com/ismlEraslan/email-summarizer-win32/releases";

        public RebrandingNoticeDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Kerkenez Mail - Migration & Rebranding Notice";
            this.Size = new Size(620, 420);
            this.MinimumSize = new Size(580, 380);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                RowCount = 6,
                ColumnCount = 1
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Header
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Subtitle / Details
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Paths card
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Winget Command Box
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Spacer
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Buttons row

            // 1. Header with Icon
            var lblTitle = new Label
            {
                Text = "🦅  Email Summarizer is now Kerkenez Mail!",
                Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 25, 35),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };

            // 2. Explanatory subtitle
            var lblSubtitle = new Label
            {
                Text = "Your accounts, preferences, and shortcuts have been safely migrated.\nThis transition release (v0.5.0-bridge) maintains temporary compatibility.",
                ForeColor = Color.FromArgb(80, 85, 95),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 16)
            };

            // 3. Storage Paths Box
            var pnlPaths = new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 14),
                Dock = DockStyle.Top,
                AutoSize = true
            };

            var lblPaths = new Label
            {
                Text = "• Shared Suite Accounts: %APPDATA%\\Kerkenez\\accounts.dat (Shared with KerkenezCalendar)\n• Mail Configuration:      %APPDATA%\\Kerkenez\\mail\\config.json",
                Font = new Font("Consolas", 8.5F),
                ForeColor = Color.FromArgb(40, 45, 55),
                AutoSize = true,
                Dock = DockStyle.Top
            };
            pnlPaths.Controls.Add(lblPaths);

            // 4. WinGet Command Box
            var pnlCommand = new Panel
            {
                BackColor = Color.FromArgb(30, 35, 45),
                Padding = new Padding(12, 10, 12, 10),
                Margin = new Padding(0, 0, 0, 16),
                Dock = DockStyle.Top,
                AutoSize = true
            };

            var lblCommand = new Label
            {
                Text = "winget install ismlEraslan.KerkenezMail",
                Font = new Font("Consolas", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(230, 240, 255),
                AutoSize = true,
                Dock = DockStyle.Top
            };
            pnlCommand.Controls.Add(lblCommand);

            // 5. Actions Button Panel
            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };

            var btnCopy = new Button
            {
                Text = "📋 Copy WinGet Command",
                AutoSize = true,
                Height = 34,
                Padding = new Padding(12, 0, 12, 0),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.System
            };
            btnCopy.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(WinGetCommand);
                    btnCopy.Text = "✓ Copied to Clipboard!";
                }
                catch { }
            };

            var btnGitHub = new Button
            {
                Text = "🌐 GitHub Releases",
                AutoSize = true,
                Height = 34,
                Padding = new Padding(12, 0, 12, 0),
                Margin = new Padding(8, 0, 0, 0),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.System
            };
            btnGitHub.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(GitHubReleasesUrl) { UseShellExecute = true });
                }
                catch { }
            };

            var btnContinue = new Button
            {
                Text = "Continue to App ➔",
                AutoSize = true,
                Height = 34,
                Padding = new Padding(14, 0, 14, 0),
                Margin = new Padding(16, 0, 0, 0),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.System
            };
            btnContinue.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            pnlButtons.Controls.Add(btnCopy);
            pnlButtons.Controls.Add(btnGitHub);
            pnlButtons.Controls.Add(btnContinue);

            mainPanel.Controls.Add(lblTitle);
            mainPanel.Controls.Add(lblSubtitle);
            mainPanel.Controls.Add(pnlPaths);
            mainPanel.Controls.Add(pnlCommand);
            mainPanel.Controls.Add(new Panel());
            mainPanel.Controls.Add(pnlButtons);

            this.Controls.Add(mainPanel);
        }
    }
}