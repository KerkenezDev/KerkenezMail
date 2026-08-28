using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EmailSummarizer.Models;

namespace EmailSummarizer.Services
{
    public class NativeTrayDaemon : IDisposable
    {
        public const string ExitEventName = @"Global\EmailSummarizer_TrayDaemon_ExitEvent";

        private const uint TRAY_ICON_ID = 1001;
        private const uint CMD_OPEN = 2001;
        private const uint CMD_CHECK_NOW = 2002;
        private const uint CMD_TOGGLE_NOTIFS = 2003;
        private const uint CMD_EXIT = 2004;

        private readonly ConfigService _configService;
        private readonly TrayDaemonService _daemonService;
        private System.Threading.Timer? _idleTrimTimer;
        private EventWaitHandle? _exitEvent;
        private RegisteredWaitHandle? _registeredWait;
        private IntPtr _hWnd = IntPtr.Zero;
        private NativeMethods.WndProcDelegate? _wndProcDelegate;
        private NativeMethods.NOTIFYICONDATA _nid;
        private bool _isDisposed;

        public NativeTrayDaemon()
        {
            _configService = new ConfigService();
            _daemonService = new TrayDaemonService(_configService);
        }

        public static void Run()
        {
            using var daemon = new NativeTrayDaemon();
            daemon.Start();
        }

        public void Start()
        {
            InitializeMessageWindow();
            InitializeTrayIcon();
            InitializeExitEventHandler();

            _daemonService.UnreadStatusUpdated += OnUnreadStatusUpdated;
            _daemonService.NewUnreadEmailsDiscovered += OnNewUnreadEmailsDiscovered;

            _daemonService.Start();

            // Periodic idle memory trim to keep active working set minimized
            _idleTrimTimer = new System.Threading.Timer(_ => NativeMethods.TrimWorkingSet(), null, 3000, 15000);

            // Native Win32 Message Loop (Zero WinForms control memory overhead)
            while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }

        private void InitializeExitEventHandler()
        {
            try
            {
                _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName);
                _registeredWait = ThreadPool.RegisterWaitForSingleObject(
                    _exitEvent,
                    (state, timedOut) =>
                    {
                        if (!timedOut && _hWnd != IntPtr.Zero)
                        {
                            NativeMethods.PostMessage(_hWnd, NativeMethods.WM_DESTROY, IntPtr.Zero, IntPtr.Zero);
                        }
                    },
                    null,
                    -1,
                    false);
            }
            catch
            {
                // Fallback
            }
        }

        private void InitializeMessageWindow()
        {
            string className = "EmailSummarizer_TrayMsgHost_" + Guid.NewGuid().ToString("N");
            _wndProcDelegate = WndProc;

            var wcx = new NativeMethods.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                hInstance = NativeMethods.GetModuleHandle(null),
                lpszClassName = className
            };

            NativeMethods.RegisterClassEx(ref wcx);

            _hWnd = NativeMethods.CreateWindowEx(
                0,
                className,
                "EmailSummarizerTrayHost",
                0,
                0, 0, 0, 0,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.GetModuleHandle(null),
                IntPtr.Zero);
        }

        private void InitializeTrayIcon()
        {
            _nid = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                hWnd = _hWnd,
                uID = TRAY_ICON_ID,
                uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
                uCallbackMessage = NativeMethods.WM_TRAYICON,
                hIcon = TrayIconHelper.GetNormalIcon().Handle,
                szTip = "Email Summarizer"
            };

            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref _nid);
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == NativeMethods.WM_TRAYICON)
            {
                uint eventMsg = (uint)(lParam.ToInt64() & 0xFFFF);

                if (eventMsg == NativeMethods.WM_LBUTTONDBLCLK || eventMsg == NativeMethods.WM_LBUTTONUP)
                {
                    LaunchOrFocusMainApp();
                    return IntPtr.Zero;
                }
                else if (eventMsg == NativeMethods.NIN_BALLOONUSERCLICK)
                {
                    LaunchOrFocusMainApp();
                    return IntPtr.Zero;
                }
                else if (eventMsg == NativeMethods.WM_RBUTTONUP)
                {
                    ShowNativeContextMenu();
                    return IntPtr.Zero;
                }
            }
            else if (msg == NativeMethods.WM_DESTROY)
            {
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
            }

            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private void ShowNativeContextMenu()
        {
            NativeMethods.GetCursorPos(out var pt);
            NativeMethods.SetForegroundWindow(_hWnd);

            IntPtr hMenu = NativeMethods.CreatePopupMenu();
            try
            {
                NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, CMD_OPEN, "📬  Open Email Summarizer");
                NativeMethods.SetMenuDefaultItem(hMenu, CMD_OPEN, 0); // Bold default item

                NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, CMD_CHECK_NOW, "🔄  Check Emails Now");

                string notifText = _configService.Settings.EnableTrayNotifications
                    ? "🔔  Notifications: Enabled"
                    : "🔕  Notifications: Disabled";
                NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, CMD_TOGGLE_NOTIFS, notifText);

                NativeMethods.AppendMenu(hMenu, NativeMethods.MF_SEPARATOR, 0, null);
                NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, CMD_EXIT, "❌  Exit Tray Daemon");

                uint cmd = NativeMethods.TrackPopupMenuEx(
                    hMenu,
                    NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_NONOTIFY | NativeMethods.TPM_RIGHTBUTTON,
                    pt.X,
                    pt.Y,
                    _hWnd,
                    IntPtr.Zero);

                HandleMenuCommand(cmd);
            }
            finally
            {
                NativeMethods.DestroyMenu(hMenu);
            }
        }

        private void HandleMenuCommand(uint cmd)
        {
            if (cmd == CMD_OPEN)
            {
                LaunchOrFocusMainApp();
            }
            else if (cmd == CMD_CHECK_NOW)
            {
                _nid.szTip = "Email Summarizer - Checking...";
                _nid.uFlags = NativeMethods.NIF_TIP;
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref _nid);

                _ = _daemonService.TriggerCheckNowAsync();
            }
            else if (cmd == CMD_TOGGLE_NOTIFS)
            {
                _configService.Settings.EnableTrayNotifications = !_configService.Settings.EnableTrayNotifications;
                _configService.SaveConfig();
            }
            else if (cmd == CMD_EXIT)
            {
                Dispose();
                NativeMethods.PostQuitMessage(0);
            }
        }

        private void OnUnreadStatusUpdated(int unreadCount, string status)
        {
            try
            {
                _nid.uFlags = NativeMethods.NIF_ICON | NativeMethods.NIF_TIP;
                _nid.hIcon = unreadCount > 0 
                    ? TrayIconHelper.GetUnreadIcon().Handle 
                    : TrayIconHelper.GetNormalIcon().Handle;

                string tip = unreadCount > 0 
                    ? $"Email Summarizer: {unreadCount} unread" 
                    : "Email Summarizer: 0 unread";

                if (tip.Length > 120) tip = tip.Substring(0, 117) + "...";
                _nid.szTip = tip;

                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref _nid);
            }
            catch
            {
                // Ignore transient update errors
            }
        }

        private void OnNewUnreadEmailsDiscovered(List<UnreadNotificationInfo> newEmails)
        {
            if (newEmails.Count == 0) return;

            try
            {
                string title;
                string message;

                if (newEmails.Count == 1)
                {
                    var email = newEmails[0];
                    string cleanSender = email.Sender.Length > 35 ? email.Sender.Substring(0, 32) + "..." : email.Sender;
                    string cleanSubject = email.Subject.Length > 50 ? email.Subject.Substring(0, 47) + "..." : email.Subject;

                    title = $"📬 New Email: {cleanSender}";
                    message = cleanSubject;
                }
                else
                {
                    title = $"📬 {newEmails.Count} New Emails";
                    var first = newEmails[0];
                    message = $"Latest from {first.Sender}: {first.Subject}";
                }

                if (title.Length > 60) title = title.Substring(0, 57) + "...";
                if (message.Length > 240) message = message.Substring(0, 237) + "...";

                _nid.uFlags = NativeMethods.NIF_INFO;
                _nid.szInfoTitle = title;
                _nid.szInfo = message;
                _nid.dwInfoFlags = NativeMethods.NIIF_INFO;

                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref _nid);
            }
            catch
            {
                // Fallback
            }
        }

        private void LaunchOrFocusMainApp()
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
                Debug.WriteLine($"[NativeTrayDaemon] LaunchMainApp failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                _nid.uFlags = 0;
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref _nid);

                if (_hWnd != IntPtr.Zero)
                {
                    NativeMethods.DestroyWindow(_hWnd);
                    _hWnd = IntPtr.Zero;
                }

                _registeredWait?.Unregister(null);
                _registeredWait = null;
                _exitEvent?.Dispose();
                _exitEvent = null;

                _idleTrimTimer?.Dispose();
                _idleTrimTimer = null;
                _daemonService.Dispose();
            }
            catch
            {
                // Fallback
            }
        }
    }
}
