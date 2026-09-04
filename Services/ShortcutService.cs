using System;
using System.IO;
using System.Windows.Forms;

namespace KerkenezMail.Services
{
    public static class ShortcutService
    {
        public static string StartMenuShortcutPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "Kerkenez Mail.lnk");

        public static string DesktopShortcutPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "Kerkenez Mail.lnk");

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
                    startMenuShortcut.Description = "Kerkenez Mail - AI Email Assistant";
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
                    desktopShortcut.Description = "Kerkenez Mail - AI Email Assistant";
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

        public static void UpdateShortcutsIfMoved()
        {
            try
            {
                bool hasStartMenu = File.Exists(StartMenuShortcutPath);
                bool hasDesktop = File.Exists(DesktopShortcutPath);

                if (hasStartMenu || hasDesktop)
                {
                    CreateShortcuts(createDesktop: hasDesktop, createStartMenu: hasStartMenu);
                }
            }
            catch { }
        }

        public static void DeleteShortcuts()
        {
            var candidatePaths = new[]
            {
                StartMenuShortcutPath,
                DesktopShortcutPath,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Kerkenez Mail.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Kerkenez Mail.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Kerkenez Mail.lnk"),
                // Legacy shortcuts cleanup
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Email Summarizer.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Email Summarizer.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Email Summarizer.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Email Summarizer.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Email Summarizer.lnk")
            };

            foreach (var path in candidatePaths)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShortcutService] Error deleting shortcut at '{path}': {ex.Message}");
                }
            }
        }
    }
}
