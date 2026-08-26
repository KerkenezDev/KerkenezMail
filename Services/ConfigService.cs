using System;
using System.IO;
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
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error loading config: {ex.Message}");
            }

            // If config doesn't exist, create default with preloaded accounts and save
            var defaults = AppSettings.CreateDefault();
            SaveConfig(defaults);
            return defaults;
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

        public static bool Uninstall()
        {
            try
            {
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
