using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using KerkenezMail.Models;

namespace KerkenezMail.Services
{
    public class ConfigService
    {
        public static readonly string SuiteFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kerkenez");

        public static readonly string AppDataFolder = Path.Combine(SuiteFolder, "mail");

        public static readonly string TempFolder = Path.Combine(
            Path.GetTempPath(),
            "Kerkenez", "mail");

        public static readonly string ConfigFilePath = Path.Combine(AppDataFolder, "config.json");
        public static readonly string AccountsFilePath = Path.Combine(SuiteFolder, "accounts.dat");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// True if config.json did not exist when this application process booted.
        /// </summary>
        public static bool IsFirstInstallation { get; } = !File.Exists(ConfigFilePath);

        public AppSettings Settings { get; private set; }

        public event Action? SettingsChanged;

        public ConfigService()
        {
            EnsureAppDataDirectory();
            Settings = LoadConfig();
        }

        private static void EnsureAppDataDirectory()
        {
            try
            {
                if (!Directory.Exists(SuiteFolder))
                {
                    Directory.CreateDirectory(SuiteFolder);
                }

                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                // Check for migration from local directory
                string localConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (!File.Exists(ConfigFilePath) && File.Exists(localConfig))
                {
                    File.Copy(localConfig, ConfigFilePath, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Directory init error: {ex.Message}");
            }
        }

        public AppSettings LoadConfig()
        {
            try
            {
                EnsureAppDataDirectory();

                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    
                    // Check for backward compatibility: legacy unencrypted accounts inside config.json
                    CheckAndMigrateLegacyConfig(json);

                    // Re-read config in case migration modified it
                    json = File.ReadAllText(ConfigFilePath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (loaded != null)
                    {
                        Settings = HealAndNormalizeSettings(loaded);
                        return Settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error loading config: {ex.Message}");
            }

            // If config doesn't exist, create default and save
            var defaults = AppSettings.CreateDefault();
            SaveConfig(defaults);
            return defaults;
        }

        private static AppSettings HealAndNormalizeSettings(AppSettings s)
        {
            if (s == null) return AppSettings.CreateDefault();

            // 1. Heal AI Backend
            if (string.IsNullOrWhiteSpace(s.AiBackend))
            {
                s.AiBackend = "LlamaCpp";
            }

            // 2. Heal llama.cpp settings
            if (s.LlamaServerPort <= 0) s.LlamaServerPort = 8080;
            if (string.IsNullOrWhiteSpace(s.LlamaServerUrl))
            {
                s.LlamaServerUrl = $"http://127.0.0.1:{s.LlamaServerPort}/v1/chat/completions";
            }
            if (s.LlamaGpuLayers < 0) s.LlamaGpuLayers = 99;

            // 3. Heal Ollama settings
            if (string.IsNullOrWhiteSpace(s.OllamaServerUrl))
            {
                s.OllamaServerUrl = "http://127.0.0.1:11434/v1/chat/completions";
            }
            if (string.IsNullOrWhiteSpace(s.OllamaModelName))
            {
                s.OllamaModelName = "llama3.2";
            }

            // 4. Heal Cloud settings
            if (string.IsNullOrWhiteSpace(s.CloudApiUrl))
            {
                s.CloudApiUrl = "https://api.openai.com/v1/chat/completions";
            }
            if (string.IsNullOrWhiteSpace(s.CloudModelName))
            {
                s.CloudModelName = "gpt-4o-mini";
            }
            if (s.CloudApiKey == null)
            {
                s.CloudApiKey = "";
            }

            // 5. Heal Global Inference settings
            if (s.MaxTokens <= 0) s.MaxTokens = 350;
            if (s.Temperature < 0.0 || s.Temperature > 2.0) s.Temperature = 0.2;
            if (s.MaxSummaryEmailChars < 0) s.MaxSummaryEmailChars = 0;
            else if (s.MaxSummaryEmailChars > 0 && s.MaxSummaryEmailChars < 500) s.MaxSummaryEmailChars = 500;

            // 6. Heal Email / System settings
            if (s.MaxEmailsPerAccount <= 0) s.MaxEmailsPerAccount = 15;
            if (s.TrayRefreshIntervalMinutes <= 0) s.TrayRefreshIntervalMinutes = 5;
            if (string.IsNullOrWhiteSpace(s.SystemPrompt) || 
                !s.SystemPrompt.Contains("Priority", StringComparison.OrdinalIgnoreCase) ||
                (s.SystemPrompt.Contains("Action required", StringComparison.OrdinalIgnoreCase) && !s.SystemPrompt.Contains("Priority 2 (Normal - DEFAULT", StringComparison.OrdinalIgnoreCase)))
            {
                s.SystemPrompt = AppSettings.CreateDefault().SystemPrompt;
            }
            if (s.AccountIds == null) s.AccountIds = new List<string>();

            return s;
        }

        private void CheckAndMigrateLegacyConfig(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Check if config.json contains legacy unencrypted Accounts array with object items
                if (root.TryGetProperty("Accounts", out var accountsProp) && accountsProp.ValueKind == JsonValueKind.Array)
                {
                    var legacyAccounts = new List<EmailAccount>();

                    foreach (var item in accountsProp.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            try
                            {
                                var acc = JsonSerializer.Deserialize<EmailAccount>(item.GetRawText(), JsonOptions);
                                if (acc != null)
                                {
                                    legacyAccounts.Add(acc);
                                }
                            }
                            catch { }
                        }
                    }

                    if (legacyAccounts.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ConfigService] Migrating {legacyAccounts.Count} legacy accounts to encrypted storage...");

                        // If accounts.dat already exists, merge accounts without duplicating
                        if (File.Exists(AccountsFilePath))
                        {
                            var existing = AccountCryptoService.LoadFromEncryptedFile(AccountsFilePath);
                            foreach (var leg in legacyAccounts)
                            {
                                if (!existing.Any(e => e.Id == leg.Id || (string.Equals(e.Email, leg.Email, StringComparison.OrdinalIgnoreCase) && string.Equals(e.Host, leg.Host, StringComparison.OrdinalIgnoreCase))))
                                {
                                    existing.Add(leg);
                                }
                            }
                            AccountCryptoService.SaveToEncryptedFile(AccountsFilePath, existing);
                        }
                        else
                        {
                            AccountCryptoService.SaveToEncryptedFile(AccountsFilePath, legacyAccounts);
                        }

                        // Load current settings and strip unencrypted accounts, saving only AccountIds
                        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? AppSettings.CreateDefault();
                        settings.AccountIds = legacyAccounts.Select(a => a.Id).Distinct().ToList();
                        
                        string cleanJson = JsonSerializer.Serialize(settings, JsonOptions);
                        File.WriteAllText(ConfigFilePath, cleanJson);
                        System.Diagnostics.Debug.WriteLine("[ConfigService] Legacy migration complete. config.json updated with AccountIds only.");
                    }
                }

                // Check if config.json contains legacy unencrypted CloudApiKey
                if (root.TryGetProperty("CloudApiKey", out var apiKeyProp) && apiKeyProp.ValueKind == JsonValueKind.String)
                {
                    string legacyPlainKey = apiKeyProp.GetString() ?? "";
                    if (!string.IsNullOrEmpty(legacyPlainKey))
                    {
                        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? AppSettings.CreateDefault();
                        settings.CloudApiKey = legacyPlainKey; // In-memory plaintext is encrypted on disk via CloudApiKeyEncrypted
                        string encryptedJson = JsonSerializer.Serialize(settings, JsonOptions);
                        File.WriteAllText(ConfigFilePath, encryptedJson);
                        System.Diagnostics.Debug.WriteLine("[ConfigService] Legacy plaintext CloudApiKey encrypted on disk.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Legacy migration error: {ex.Message}");
            }
        }

        public bool SaveConfig(AppSettings? settingsToSave = null)
        {
            try
            {
                EnsureAppDataDirectory();

                if (settingsToSave != null)
                {
                    Settings = settingsToSave;
                }

                string json = JsonSerializer.Serialize(Settings, JsonOptions);
                File.WriteAllText(ConfigFilePath, json);
                SettingsChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error saving config: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieves and decrypts the email accounts from encrypted AppData storage.
        /// </summary>
        public List<EmailAccount> GetAccounts()
        {
            try
            {
                EnsureAppDataDirectory();

                if (File.Exists(AccountsFilePath))
                {
                    return AccountCryptoService.LoadFromEncryptedFile(AccountsFilePath);
                }

                // If accounts.dat does not exist yet, check if config.json has legacy accounts to migrate
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    CheckAndMigrateLegacyConfig(json);

                    if (File.Exists(AccountsFilePath))
                    {
                        return AccountCryptoService.LoadFromEncryptedFile(AccountsFilePath);
                    }
                }

                return new List<EmailAccount>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error in GetAccounts: {ex.Message}");
                return new List<EmailAccount>();
            }
        }

        /// <summary>
        /// Encrypts and persists the email accounts to accounts.dat, updating AccountIds in config.json.
        /// </summary>
        public bool SaveAccounts(List<EmailAccount> accounts)
        {
            try
            {
                EnsureAppDataDirectory();

                if (accounts == null) accounts = new List<EmailAccount>();

                bool saved = AccountCryptoService.SaveToEncryptedFile(AccountsFilePath, accounts);
                if (saved)
                {
                    // Sync AccountIds into config.json
                    Settings.AccountIds = accounts.Select(a => a.Id).ToList();
                    SaveConfig();
                    SettingsChanged?.Invoke();
                }

                return saved;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error saving accounts: {ex.Message}");
                return false;
            }
        }

        public static bool Uninstall()
        {
            try
            {
                // Remove Windows logon startup registry entry
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    key?.DeleteValue("KerkenezMailTray", false);
                    key?.DeleteValue("EmailSummarizerTray", false);
                }
                catch { }

                // Remove Windows Uninstall / Add-Remove Programs registration
                UninstallRegistrationService.Unregister();

                // Remove Desktop and Start Menu shortcuts
                ShortcutService.DeleteShortcuts();

                // Clean temporary folder
                CleanTempFolder();

                if (Directory.Exists(AppDataFolder))
                {
                    Directory.Delete(AppDataFolder, true);
                    return true;
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error during uninstall: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cleans up temporary preview and cache files stored in %TEMP%\Kerkenez\mail.
        /// Handles locked files gracefully without throwing exceptions.
        /// </summary>
        public static void CleanTempFolder()
        {
            try
            {
                if (!Directory.Exists(TempFolder)) return;

                var dirInfo = new DirectoryInfo(TempFolder);

                // Delete all files inside temp folder
                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        file.Attributes = FileAttributes.Normal;
                        file.Delete();
                    }
                    catch
                    {
                        // File may be locked by another process (e.g. browser); best effort
                    }
                }

                // Delete all subdirectories if any
                foreach (var subDir in dirInfo.EnumerateDirectories("*", SearchOption.AllDirectories).OrderByDescending(d => d.FullName.Length))
                {
                    try
                    {
                        subDir.Delete(false);
                    }
                    catch
                    {
                        // Best effort
                    }
                }

                // If directory is now empty, remove it
                try
                {
                    if (!dirInfo.EnumerateFileSystemInfos().Any())
                    {
                        Directory.Delete(TempFolder, false);
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] CleanTempFolder error: {ex.Message}");
            }
        }
    }
}
