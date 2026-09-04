using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace KerkenezMail.Services
{
    public static class UninstallRegistrationService
    {
        private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\KerkenezMail";
        private const string LegacyUninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\EmailSummarizer";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "KerkenezMailTray";
        private const string LegacyRunValueName1 = "EmailSummarizerTray";
        private const string LegacyRunValueName2 = "EmailSummarizer";
        private const string DisplayName = "Kerkenez Mail";
        private const string Publisher = "ismlEraslan";
        private const string UrlInfoAbout = "https://github.com/ismlEraslan/KerkenezMail";
        private const string HelpLink = "https://github.com/ismlEraslan/KerkenezMail";

        /// <summary>
        /// Gets the active application version dynamically from assembly metadata (e.g. "1.0.0").
        /// Automatically reflects changes to &lt;Version&gt; in KerkenezMail.csproj upon new builds/releases.
        /// </summary>
        public static string CurrentVersion
        {
            get
            {
                try
                {
                    var infoVer = typeof(UninstallRegistrationService).Assembly
                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                        .InformationalVersion;

                    if (!string.IsNullOrWhiteSpace(infoVer))
                    {
                        int plusIdx = infoVer.IndexOf('+');
                        string clean = plusIdx > 0 ? infoVer.Substring(0, plusIdx) : infoVer;
                        clean = clean.Trim();
                        if (!string.IsNullOrEmpty(clean)) return clean;
                    }
                }
                catch { }

                try
                {
                    string prodVer = Application.ProductVersion;
                    if (!string.IsNullOrWhiteSpace(prodVer))
                    {
                        int plusIdx = prodVer.IndexOf('+');
                        string clean = plusIdx > 0 ? prodVer.Substring(0, plusIdx) : prodVer;
                        clean = clean.Trim();
                        if (!string.IsNullOrEmpty(clean)) return clean;
                    }
                }
                catch { }

                try
                {
                    var ver = typeof(UninstallRegistrationService).Assembly.GetName().Version;
                    if (ver != null)
                    {
                        return $"{ver.Major}.{ver.Minor}.{Math.Max(0, ver.Build)}";
                    }
                }
                catch { }

                return "1.0.0";
            }
        }

        /// <summary>
        /// Registers or updates the application in HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\KerkenezMail
        /// and checks/updates the HKCU Run startup entry if the app executable path was moved.
        /// </summary>
        public static void RegisterOrUpdate()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return;

                // 1. Update Uninstall Entry for Windows Installed Apps & Revo
                UpdateUninstallEntry(exePath);

                // 2. Update HKCU Run Startup Key if enabled/configured
                UpdateStartupRunKey(exePath);

                // 3. Update existing shortcuts if present
                ShortcutService.UpdateShortcutsIfMoved();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UninstallRegistrationService] Error during register/update: {ex.Message}");
            }
        }

        private static void UpdateUninstallEntry(string exePath)
        {
            try
            {
                string installDir = Path.GetDirectoryName(exePath) ?? "";
                string uninstallCmd = $"\"{exePath}\" --uninstall";
                string quietUninstallCmd = $"\"{exePath}\" --uninstall --quiet";
                string displayIcon = $"\"{exePath}\",0";
                string activeVersion = CurrentVersion;

                using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath, true);
                if (key != null)
                {
                    var currentUninstall = key.GetValue("UninstallString") as string;
                    var currentQuiet = key.GetValue("QuietUninstallString") as string;
                    var currentLocation = key.GetValue("InstallLocation") as string;
                    var currentIcon = key.GetValue("DisplayIcon") as string;
                    var currentRegVersion = key.GetValue("DisplayVersion") as string;

                    // Only write if there's a difference or new key to avoid unnecessary disk/registry writes
                    if (!string.Equals(currentUninstall, uninstallCmd, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(currentQuiet, quietUninstallCmd, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(currentLocation, installDir, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(currentIcon, displayIcon, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(currentRegVersion?.TrimStart('v', 'V'), activeVersion.TrimStart('v', 'V'), StringComparison.OrdinalIgnoreCase))
                    {
                        key.SetValue("DisplayName", DisplayName);
                        key.SetValue("DisplayVersion", activeVersion);
                        key.SetValue("Publisher", Publisher);
                        key.SetValue("DisplayIcon", displayIcon);
                        key.SetValue("InstallLocation", installDir);
                        key.SetValue("UninstallString", uninstallCmd);
                        key.SetValue("QuietUninstallString", quietUninstallCmd);
                        key.SetValue("URLInfoAbout", UrlInfoAbout);
                        key.SetValue("HelpLink", HelpLink);
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);

                        if (Version.TryParse(activeVersion.TrimStart('v', 'V'), out var parsedVer))
                        {
                            key.SetValue("MajorVersion", parsedVer.Major, RegistryValueKind.DWord);
                            key.SetValue("MinorVersion", parsedVer.Minor, RegistryValueKind.DWord);
                            key.SetValue("Version", (parsedVer.Major << 24) | (parsedVer.Minor << 16) | Math.Max(0, parsedVer.Build), RegistryValueKind.DWord);
                        }

                        if (key.GetValue("InstallDate") == null)
                        {
                            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                        }

                        try
                        {
                            var fi = new FileInfo(exePath);
                            long sizeKb = fi.Length / 1024;
                            key.SetValue("EstimatedSize", (int)sizeKb, RegistryValueKind.DWord);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UninstallRegistrationService] Error updating uninstall entry: {ex.Message}");
            }
        }

        private static void UpdateStartupRunKey(string exePath)
        {
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (runKey == null) return;

                var currentVal = runKey.GetValue(RunValueName) as string;
                var legacyVal1 = runKey.GetValue(LegacyRunValueName1) as string;
                var legacyVal2 = runKey.GetValue(LegacyRunValueName2) as string;

                // Check if startup was configured under modern or legacy key name
                if (!string.IsNullOrWhiteSpace(currentVal) || !string.IsNullOrWhiteSpace(legacyVal1) || !string.IsNullOrWhiteSpace(legacyVal2))
                {
                    string expectedVal = $"\"{exePath}\" --daemon";

                    if (!string.Equals(currentVal, expectedVal, StringComparison.OrdinalIgnoreCase))
                    {
                        runKey.SetValue(RunValueName, expectedVal);
                    }

                    // Clean up legacy key names if they existed
                    if (!string.IsNullOrWhiteSpace(legacyVal1))
                    {
                        runKey.DeleteValue(LegacyRunValueName1, false);
                    }
                    if (!string.IsNullOrWhiteSpace(legacyVal2))
                    {
                        runKey.DeleteValue(LegacyRunValueName2, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UninstallRegistrationService] Error updating startup run key: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes the application registration key from HKCU Uninstall registry.
        /// </summary>
        public static void Unregister()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UninstallRegistrationService] Error unregistering app: {ex.Message}");
            }

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(LegacyUninstallKeyPath, false);
            }
            catch { }
        }
    }
}
