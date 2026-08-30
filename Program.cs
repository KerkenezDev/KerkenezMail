using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using EmailSummarizer.Services;
using EmailSummarizer.UI;

namespace EmailSummarizer
{
    static class Program
    {
        private const string TrayDaemonMutexName = @"Global\EmailSummarizer_TrayDaemon_Mutex";
        private const string MainUiMutexName = @"Global\EmailSummarizer_MainUI_Mutex";

        [STAThread]
        static void Main(string[] args)
        {
            // 1. Handle --uninstall switch
            if (args != null && args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                                              a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase) ||
                                              a.Equals("-uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                bool isQuiet = args.Any(a => a.Equals("--quiet", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("/quiet", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("-quiet", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("/silent", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("-silent", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("-s", StringComparison.OrdinalIgnoreCase) ||
                                             a.Equals("-q", StringComparison.OrdinalIgnoreCase));

                if (!isQuiet)
                {
                    ApplicationConfiguration.Initialize();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                }
                HandleUninstall(isQuiet);
                return;
            }

            // Always ensure application registration in HKCU Uninstall key is created/updated (e.g. if app moved)
            UninstallRegistrationService.RegisterOrUpdate();

            // 2. Handle --daemon or --tray switch (Background System Tray Daemon)
            bool isDaemonMode = args != null && args.Any(a => a.Equals("--daemon", StringComparison.OrdinalIgnoreCase) ||
                                                              a.Equals("/daemon", StringComparison.OrdinalIgnoreCase) ||
                                                              a.Equals("-daemon", StringComparison.OrdinalIgnoreCase) ||
                                                              a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                                                              a.Equals("/tray", StringComparison.OrdinalIgnoreCase) ||
                                                              a.Equals("-tray", StringComparison.OrdinalIgnoreCase));

            if (isDaemonMode)
            {
                RunDaemonMode();
                return;
            }

            // 3. Normal Execution (Main GUI Application)
            RunMainUiMode();
        }

        private static void RunDaemonMode()
        {
            // Acquire single-instance mutex for tray daemon
            using var mutex = new Mutex(true, TrayDaemonMutexName, out bool createdNew);
            if (!createdNew)
            {
                // Another daemon instance is already active
                return;
            }

            try
            {
                NativeTrayDaemon.Run();
            }
            finally
            {
                try { mutex.ReleaseMutex(); } catch { }
            }
        }

        private static void RunMainUiMode()
        {
            // Check if another instance of the Main UI is already running
            using var mainMutex = new Mutex(true, MainUiMutexName, out bool createdNewMain);
            if (!createdNewMain)
            {
                // Focus the existing main window and exit
                FocusExistingMainWindow();
                return;
            }

            try
            {
                // Check if background tray daemon should be active
                var configService = new ConfigService();
                if (configService.Settings.AlwaysKeepOn)
                {
                    StartDaemonIfNotRunning();
                }

                ApplicationConfiguration.Initialize();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Application.Run(new MainForm(configService));
            }
            finally
            {
                try { mainMutex.ReleaseMutex(); } catch { }
            }
        }

        private static void StartDaemonIfNotRunning()
        {
            try
            {
                bool daemonRunning = Mutex.TryOpenExisting(TrayDaemonMutexName, out var existingMutex);
                if (daemonRunning)
                {
                    existingMutex?.Dispose();
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = "--daemon",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch
            {
                // Fallback
            }
        }

        private static void FocusExistingMainWindow()
        {
            try
            {
                int currentPid = Process.GetCurrentProcess().Id;
                string currentExe = Application.ExecutablePath;
                string procName = Path.GetFileNameWithoutExtension(currentExe);

                var existingMain = Process.GetProcessesByName(procName)
                    .FirstOrDefault(p => p.Id != currentPid && p.MainWindowHandle != IntPtr.Zero);

                if (existingMain != null)
                {
                    NativeMethods.FocusMainWindow(existingMain);
                }
            }
            catch
            {
                // Fallback
            }
        }

        private static void HandleUninstall(bool isQuiet)
        {
            if (isQuiet)
            {
                try
                {
                    ConfigService.Uninstall();
                }
                catch { }
                return;
            }

            var res = MessageBox.Show(
                "Are you sure you want to uninstall Email Summarizer?\n\nThis will remove Desktop and Start Menu shortcuts, Windows startup entries, Add/Remove Programs registration, and delete all configuration and cached data from %APPDATA%\\EmailSummarizer.",
                "Uninstall Email Summarizer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res == DialogResult.Yes)
            {
                bool success = ConfigService.Uninstall();
                if (success)
                {
                    MessageBox.Show(
                        "Email Summarizer shortcuts, startup entries, Windows Add/Remove registration, configuration, and data have been successfully removed.",
                        "Uninstall Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Failed to completely remove all configuration files or shortcuts.",
                        "Uninstall Warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }
    }
}