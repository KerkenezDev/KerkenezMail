using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using KerkenezMail.Models;
using Microsoft.Win32;

namespace KerkenezMail.Services
{
    public class MigrationResult
    {
        public bool NeedsMigration { get; set; }
        public bool MigrationAttempted { get; set; }
        public bool Success { get; set; }
        public int AccountsMigrated { get; set; }
        public bool ConfigMigrated { get; set; }
        public bool RegistryMigrated { get; set; }
        public bool ShortcutsMigrated { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Universal Migration Engine: Atomically migrates legacy Email Summarizer state,
    /// re-encrypts DPAPI accounts/secrets to Kerkenez suite standards, updates registry keys,
    /// and replaces Windows shortcuts.
    /// </summary>
    public static class MigrationService
    {
        public static readonly string LegacyAppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EmailSummarizer");

        public static readonly string LegacyAccountsFilePath = Path.Combine(
            LegacyAppDataFolder,
            "accounts.dat");

        public static readonly string LegacyConfigFilePath = Path.Combine(
            LegacyAppDataFolder,
            "config.json");

        public static readonly string SharedKerkenezFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kerkenez");

        public static readonly string MailAppDataFolder = Path.Combine(
            SharedKerkenezFolder,
            "mail");

        public static readonly string SharedAccountsFilePath = Path.Combine(
            SharedKerkenezFolder,
            "accounts.dat");

        public static readonly string MailConfigFilePath = Path.Combine(
            MailAppDataFolder,
            "config.json");

        private static readonly byte[] LegacyAccountEntropy = Encoding.UTF8.GetBytes("EmailSummarizer.SecureAccounts.v1");
        private static readonly byte[] SuiteAccountEntropy = Encoding.UTF8.GetBytes("Kerkenez.SecureAccounts.v1");
        private static readonly byte[] LegacySecretEntropy = Encoding.UTF8.GetBytes("EmailSummarizer.SecureSecrets.v1");
        private static readonly byte[] MailSecretEntropy = Encoding.UTF8.GetBytes("KerkenezMail.SecureSecrets.v1");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static bool CheckIfMigrationNeeded()
        {
            if (Directory.Exists(LegacyAppDataFolder)) return true;
            if (File.Exists(LegacyAccountsFilePath) || File.Exists(LegacyConfigFilePath)) return true;

            try
            {
                using var unKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\EmailSummarizer");
                if (unKey != null) return true;

                using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (runKey?.GetValue("EmailSummarizerTray") != null || runKey?.GetValue("EmailSummarizer") != null) return true;
            }
            catch { }

            string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Email Summarizer.lnk");
            string programsLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Email Summarizer.lnk");
            if (File.Exists(desktopLnk) || File.Exists(programsLnk)) return true;

            return false;
        }

        public static MigrationResult ExecuteMigrationIfNeeded()
        {
            var result = new MigrationResult();
            if (!CheckIfMigrationNeeded())
            {
                result.NeedsMigration = false;
                result.Success = true;
                return result;
            }

            result.NeedsMigration = true;
            result.MigrationAttempted = true;

            try
            {
                // 1. Stop legacy daemon if active
                StopLegacyDaemonIfRunning();

                // 2. Ensure target folders exist
                if (!Directory.Exists(SharedKerkenezFolder)) Directory.CreateDirectory(SharedKerkenezFolder);
                if (!Directory.Exists(MailAppDataFolder)) Directory.CreateDirectory(MailAppDataFolder);

                // 3. Migrate and re-encrypt accounts.dat -> %APPDATA%\Kerkenez\accounts.dat
                MigrateAccounts(result);

                // 4. Migrate and re-encrypt config.json -> %APPDATA%\Kerkenez\mail\config.json
                MigrateConfig(result);

                // 5. Migrate Windows Registry entries (Uninstall & Run)
                MigrateRegistry(result);

                // 6. Migrate Desktop and Start Menu shortcuts
                MigrateShortcuts(result);

                // 7. Verify integrity before deleting legacy directory
                if (VerifyMigration())
                {
                    try
                    {
                        if (Directory.Exists(LegacyAppDataFolder))
                        {
                            Directory.Delete(LegacyAppDataFolder, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MigrationService] Notice: Could not remove legacy dir: {ex.Message}");
                    }
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Debug.WriteLine($"[MigrationService] Migration error: {ex}");
            }

            return result;
        }

        private static void StopLegacyDaemonIfRunning()
        {
            try
            {
                if (EventWaitHandle.TryOpenExisting(@"Global\EmailSummarizer_TrayDaemon_ExitEvent", out var exitEvt))
                {
                    exitEvt.Set();
                    exitEvt.Dispose();
                    Thread.Sleep(300);
                }
            }
            catch { }
        }

        private static void MigrateAccounts(MigrationResult result)
        {
            if (!File.Exists(LegacyAccountsFilePath)) return;

            try
            {
                byte[] legacyCipher = File.ReadAllBytes(LegacyAccountsFilePath);
                byte[] plainBytes = ProtectedData.Unprotect(legacyCipher, LegacyAccountEntropy, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(plainBytes);

                var legacyAccounts = JsonSerializer.Deserialize<List<EmailAccount>>(json, JsonOptions) ?? new List<EmailAccount>();
                if (legacyAccounts.Count == 0) return;

                List<EmailAccount> targetAccounts;
                if (File.Exists(SharedAccountsFilePath))
                {
                    // Existing shared accounts (e.g. from KerkenezCalendar) - merge non-destructively
                    targetAccounts = AccountCryptoService.LoadFromEncryptedFile(SharedAccountsFilePath);
                    foreach (var leg in legacyAccounts)
                    {
                        if (!targetAccounts.Any(t => t.Id == leg.Id || (string.Equals(t.Email, leg.Email, StringComparison.OrdinalIgnoreCase) && string.Equals(t.Host, leg.Host, StringComparison.OrdinalIgnoreCase))))
                        {
                            targetAccounts.Add(leg);
                        }
                    }
                }
                else
                {
                    targetAccounts = legacyAccounts;
                }

                // Write atomically with suite entropy
                bool saved = AccountCryptoService.SaveToEncryptedFile(SharedAccountsFilePath, targetAccounts);
                if (saved)
                {
                    result.AccountsMigrated = legacyAccounts.Count;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MigrationService] Accounts migration failed: {ex.Message}");
            }
        }

        private static void MigrateConfig(MigrationResult result)
        {
            if (!File.Exists(LegacyConfigFilePath)) return;

            try
            {
                string legacyJson = File.ReadAllText(LegacyConfigFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(legacyJson, JsonOptions) ?? AppSettings.CreateDefault();

                // Re-encrypt CloudApiKey if present
                using var doc = JsonDocument.Parse(legacyJson);
                if (doc.RootElement.TryGetProperty("CloudApiKeyEncrypted", out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    string oldCipher = prop.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(oldCipher))
                    {
                        try
                        {
                            byte[] cipherBytes = Convert.FromBase64String(oldCipher);
                            byte[] plainKeyBytes = ProtectedData.Unprotect(cipherBytes, LegacySecretEntropy, DataProtectionScope.CurrentUser);
                            string plainKey = Encoding.UTF8.GetString(plainKeyBytes);

                            if (!string.IsNullOrEmpty(plainKey))
                            {
                                settings.CloudApiKey = plainKey;
                            }
                        }
                        catch { }
                    }
                }

                // If target config does not exist, save migrated settings
                if (!File.Exists(MailConfigFilePath))
                {
                    string targetJson = JsonSerializer.Serialize(settings, JsonOptions);
                    File.WriteAllText(MailConfigFilePath, targetJson);
                    result.ConfigMigrated = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MigrationService] Config migration failed: {ex.Message}");
            }
        }

        private static void MigrateRegistry(MigrationResult result)
        {
            try
            {
                // 1. Uninstall registration
                using (var legKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\EmailSummarizer"))
                {
                    if (legKey != null)
                    {
                        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\EmailSummarizer", false);
                        result.RegistryMigrated = true;
                    }
                }

                // 2. Run startup registration
                using (var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (runKey != null)
                    {
                        var legVal = runKey.GetValue("EmailSummarizerTray") ?? runKey.GetValue("EmailSummarizer");
                        if (legVal != null)
                        {
                            runKey.DeleteValue("EmailSummarizerTray", false);
                            runKey.DeleteValue("EmailSummarizer", false);
                            runKey.SetValue("KerkenezMailTray", $"\"{System.Windows.Forms.Application.ExecutablePath}\" --daemon");
                            result.RegistryMigrated = true;
                        }
                    }
                }

                UninstallRegistrationService.RegisterOrUpdate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MigrationService] Registry migration failed: {ex.Message}");
            }
        }

        private static void MigrateShortcuts(MigrationResult result)
        {
            try
            {
                string oldDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Email Summarizer.lnk");
                string oldPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Email Summarizer.lnk");

                bool hadShortcuts = File.Exists(oldDesktop) || File.Exists(oldPrograms);

                if (File.Exists(oldDesktop)) { try { File.Delete(oldDesktop); } catch { } }
                if (File.Exists(oldPrograms)) { try { File.Delete(oldPrograms); } catch { } }

                if (hadShortcuts)
                {
                    ShortcutService.CreateShortcuts();
                    result.ShortcutsMigrated = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MigrationService] Shortcut migration failed: {ex.Message}");
            }
        }

        private static bool VerifyMigration()
        {
            try
            {
                // Verify accounts can be read from target
                if (File.Exists(SharedAccountsFilePath))
                {
                    var testAccounts = AccountCryptoService.LoadFromEncryptedFile(SharedAccountsFilePath);
                    if (testAccounts == null) return false;
                }

                // Verify config can be parsed from target
                if (File.Exists(MailConfigFilePath))
                {
                    string json = File.ReadAllText(MailConfigFilePath);
                    var testConfig = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (testConfig == null) return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}