using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows.Forms;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace EmailSummarizer.Services
{
    /// <summary>
    /// Delivers persistent Windows toast notifications that remain in the Windows Action Center
    /// until dismissed or clicked by the user, with automatic fallback to system tray balloon tips.
    /// </summary>
    public static class NotificationService
    {
        private const string AppDisplayName = "Email Summarizer";
        private static string? _cachedAppId;
        private static bool _isRegistered = false;
        private static readonly object _lock = new();

        [System.Runtime.InteropServices.DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string AppID);

        /// <summary>
        /// Resolves the Windows AppID matching the registered Start Menu shortcut so
        /// Windows Action Center displays the official application icon in the header next to the app name.
        /// </summary>
        public static string ResolveAppId()
        {
            if (_cachedAppId != null) return _cachedAppId;

            try
            {
                string startMenuLnk = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    "Email Summarizer.lnk");

                if (File.Exists(startMenuLnk))
                {
                    Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellType)!;
                        dynamic shortcut = shell.CreateShortcut(startMenuLnk);
                        string target = (string)shortcut.TargetPath;
                        if (!string.IsNullOrWhiteSpace(target) && File.Exists(target))
                        {
                            return _cachedAppId = target;
                        }
                    }
                }
                else
                {
                    // Ensure shortcut is created so Windows Action Center has an identity for this app
                    ShortcutService.CreateShortcuts(createDesktop: false, createStartMenu: true);
                }
            }
            catch { }

            return _cachedAppId = Application.ExecutablePath;
        }

        public static void EnsureRegistered()
        {
            if (_isRegistered) return;

            lock (_lock)
            {
                if (_isRegistered) return;

                try
                {
                    string appId = ResolveAppId();
                    SetCurrentProcessExplicitAppUserModelID(appId);
                    _isRegistered = true;
                }
                catch { }
            }
        }

        /// <summary>
        /// Displays a notification that pops up as a banner and persists into the Windows Notification Center.
        /// Displays the official application icon next to the app name in the Action Center header without
        /// any body image clutter.
        /// If toast display fails, calls the provided fallback action (e.g. Shell_NotifyIcon balloon).
        /// </summary>
        public static bool ShowNotification(string title, string message, Action? fallbackAction = null)
        {
            try
            {
                EnsureRegistered();
                string appId = ResolveAppId();

                string safeTitle = SecurityElement.Escape(title ?? AppDisplayName);
                string safeMessage = SecurityElement.Escape(message ?? "");

                // Clean standard toast: text-only card body, app icon in header
                string toastXml = $@"
<toast duration=""short"">
    <visual>
        <binding template=""ToastGeneric"">
            <text>{safeTitle}</text>
            <text>{safeMessage}</text>
        </binding>
    </visual>
    <audio src=""ms-winsoundevent:Notification.Default"" />
</toast>";

                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(toastXml);

                var toast = new ToastNotification(xmlDoc);

                // When user clicks the toast in Action Center or the banner, bring main app to focus
                toast.Activated += (s, e) =>
                {
                    LaunchOrFocusMainApp();
                };

                var notifier = ToastNotificationManager.CreateToastNotifier(appId);
                notifier.Show(toast);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] Toast failed, invoking fallback: {ex.Message}");
                try
                {
                    fallbackAction?.Invoke();
                }
                catch { }
                return false;
            }
        }

        /// <summary>
        /// Launches or focuses the primary Email Summarizer application window.
        /// </summary>
        public static void LaunchOrFocusMainApp()
        {
            try
            {
                int currentPid = Process.GetCurrentProcess().Id;
                string currentExe = Application.ExecutablePath;
                string procName = Path.GetFileNameWithoutExtension(currentExe);

                var mainProcess = Process.GetProcessesByName(procName)
                    .FirstOrDefault(p => p.Id != currentPid && p.MainWindowHandle != IntPtr.Zero);

                if (mainProcess != null && NativeMethods.FocusMainWindow(mainProcess))
                {
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationService] LaunchOrFocusMainApp failed: {ex.Message}");
            }
        }
    }
}
