using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EmailSummarizer.Models;

namespace EmailSummarizer.Services
{
    public class ConfigService
    {
        public static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EmailSummarizer");

        public static readonly string ConfigFilePath = Path.Combine(AppDataFolder, "config.json");
        public static readonly string AccountsFilePath = Path.Combine(AppDataFolder, "accounts.dat");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

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
                        Settings = loaded;
                        return loaded;
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
                    key?.DeleteValue("EmailSummarizerTray", false);
                }
                catch { }

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
    }
}
