using System;
using System.IO;
using System.Windows.Forms;

namespace EmailSummarizer.Services
{
    public static class ShortcutService
    {
        public static string StartMenuShortcutPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "Email Summarizer.lnk");

        public static string DesktopShortcutPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Email Summarizer.lnk");

        public static bool ShortcutsExist => File.Exists(StartMenuShortcutPath) || File.Exists(DesktopShortcutPath);

        public static bool CreateShortcuts(bool createDesktop = true, bool createStartMenu = true)
        {
            try
            {
                string exePath = Application.ExecutablePath;
                if (!File.Exists(exePath)) return false;

                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;

                dynamic shell = Activator.CreateInstance(shellType)!;

                if (createStartMenu)
                {
                    string dir = Path.GetDirectoryName(StartMenuShortcutPath) ?? "";
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    dynamic startMenuShortcut = shell.CreateShortcut(StartMenuShortcutPath);
                    startMenuShortcut.TargetPath = exePath;
                    startMenuShortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                    startMenuShortcut.Description = "Email Summarizer - AI Email Assistant";
                    startMenuShortcut.IconLocation = exePath + ",0";
                    startMenuShortcut.Save();
                }

                if (createDesktop)
                {
                    string dir = Path.GetDirectoryName(DesktopShortcutPath) ?? "";
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    dynamic desktopShortcut = shell.CreateShortcut(DesktopShortcutPath);
                    desktopShortcut.TargetPath = exePath;
                    desktopShortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                    desktopShortcut.Description = "Email Summarizer - AI Email Assistant";
                    desktopShortcut.IconLocation = exePath + ",0";
                    desktopShortcut.Save();
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShortcutService] Error creating shortcuts: {ex.Message}");
                return false;
            }
        }

        public static void DeleteShortcuts()
        {
            try
            {
                if (File.Exists(StartMenuShortcutPath)) File.Delete(StartMenuShortcutPath);
                if (File.Exists(DesktopShortcutPath)) File.Delete(DesktopShortcutPath);
            }
            catch { }
        }
    }
}
