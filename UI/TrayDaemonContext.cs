using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using EmailSummarizer.Services;

namespace EmailSummarizer.UI
{
    public class TrayDaemonContext : ApplicationContext
    {
        private readonly ConfigService _configService;
        private readonly TrayDaemonService _daemonService;
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _contextMenu;
        private ToolStripMenuItem _menuNotificationsToggle = null!;

        public TrayDaemonContext()
        {
            _configService = new ConfigService();
            _daemonService = new TrayDaemonService(_configService);

            // Context Menu Setup
            _contextMenu = new ContextMenuStrip();
            InitializeContextMenu();

            // NotifyIcon Setup
            _notifyIcon = new NotifyIcon
            {
                Icon = TrayIconHelper.GetNormalIcon(),
                Text = "Email Summarizer",
                ContextMenuStrip = _contextMenu,
                Visible = true
            };

            _notifyIcon.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    LaunchOrFocusMainApp();
                }
            };

            _notifyIcon.BalloonTipClicked += (s, e) =>
            {
                LaunchOrFocusMainApp();
            };

            // Hook daemon events
            _daemonService.UnreadStatusUpdated += OnUnreadStatusUpdated;
            _daemonService.NewUnreadEmailsDiscovered += OnNewUnreadEmailsDiscovered;

            // Start background monitoring
            _daemonService.Start();

            // Initial memory trim
            NativeMethods.TrimWorkingSet();
        }

        private void InitializeContextMenu()
        {
            _contextMenu.Font = new Font("Segoe UI", 9F);

            var itemOpen = new ToolStripMenuItem("📬  Open Email Summarizer")
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            itemOpen.Click += (s, e) => LaunchOrFocusMainApp();

            var itemCheckNow = new ToolStripMenuItem("🔄  Check Emails Now");
            itemCheckNow.Click += async (s, e) =>
            {
                SetTooltip("Email Summarizer - Checking...");
                await _daemonService.TriggerCheckNowAsync();
            };

            _menuNotificationsToggle = new ToolStripMenuItem(GetNotificationsToggleText());
            _menuNotificationsToggle.Click += (s, e) =>
            {
                _configService.Settings.EnableTrayNotifications = !_configService.Settings.EnableTrayNotifications;
                _configService.SaveConfig();
                _menuNotificationsToggle.Text = GetNotificationsToggleText();
            };

            var itemExit = new ToolStripMenuItem("❌  Exit Tray Daemon");
            itemExit.Click += (s, e) =>
            {
                ExitThread();
            };

            _contextMenu.Items.Add(itemOpen);
            _contextMenu.Items.Add(itemCheckNow);
            _contextMenu.Items.Add(_menuNotificationsToggle);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(itemExit);
        }

        private string GetNotificationsToggleText()
        {
            return _configService.Settings.EnableTrayNotifications 
                ? "🔔  Notifications: Enabled" 
                : "🔕  Notifications: Disabled";
        }

        private void OnUnreadStatusUpdated(int unreadCount, string status)
        {
            try
            {
                if (_notifyIcon == null) return;

                if (unreadCount > 0)
                {
                    _notifyIcon.Icon = TrayIconHelper.GetUnreadIcon();
                    SetTooltip($"Email Summarizer: {unreadCount} unread");
                }
                else
                {
                    _notifyIcon.Icon = TrayIconHelper.GetNormalIcon();
                    SetTooltip("Email Summarizer: 0 unread");
                }
            }
            catch
            {
                // Ignore UI thread transitions
            }
        }

        private void OnNewUnreadEmailsDiscovered(System.Collections.Generic.List<UnreadNotificationInfo> newEmails)
        {
            if (newEmails.Count == 0) return;

            try
            {
                string title;
                string message;

                if (newEmails.Count == 1)
                {
                    var email = newEmails[0];
                    string cleanSender = email.Sender.Length > 40 ? email.Sender.Substring(0, 37) + "..." : email.Sender;
                    string cleanSubject = email.Subject.Length > 60 ? email.Subject.Substring(0, 57) + "..." : email.Subject;

                    title = $"📬 New Email: {cleanSender}";
                    message = cleanSubject;
                }
                else
                {
                    title = $"📬 {newEmails.Count} New Emails";
                    var first = newEmails[0];
                    message = $"Latest from {first.Sender}: {first.Subject}";
                }

                _notifyIcon.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);
            }
            catch
            {
                // Balloon tip fallback
            }
        }

        private void SetTooltip(string text)
        {
            // Windows NotifyIcon Text property is limited to 63 characters
            if (text.Length > 63)
            {
                text = text.Substring(0, 60) + "...";
            }
            _notifyIcon.Text = text;
        }

        private void LaunchOrFocusMainApp()
        {
            try
            {
                int currentPid = Process.GetCurrentProcess().Id;
                string currentExe = Application.ExecutablePath;
                string processName = Path.GetFileNameWithoutExtension(currentExe);

                // Find another running process that is the main UI (has a window title or main window handle)
                var mainProcess = Process.GetProcessesByName(processName)
                    .FirstOrDefault(p => p.Id != currentPid && p.MainWindowHandle != IntPtr.Zero);

                if (mainProcess != null && NativeMethods.FocusMainWindow(mainProcess))
                {
                    return;
                }

                // If no main window found, spawn a new instance of the application (without --daemon argument)
                Process.Start(new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrayDaemon] Failed to launch main app: {ex.Message}");
            }
        }

        protected override void ExitThreadCore()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _daemonService.Dispose();
            ConfigService.CleanTempFolder();
            base.ExitThreadCore();
        }
    }
}
