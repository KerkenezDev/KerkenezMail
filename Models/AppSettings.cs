using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using KerkenezMail.Services;

namespace KerkenezMail.Models
{
    public class AppSettings
    {
        public List<string> AccountIds { get; set; } = new List<string>();

        // Application Version ("1.0.0", "1.1.0", etc.)
        public string AppVersion { get; set; } = "";

        // Language / Localization Setting ("en", "tr", etc.)
        public string Language { get; set; } = "en";

        // AI Backend Selection ("LlamaCpp", "Ollama", "Cloud", "None")
        public string AiBackend { get; set; } = "LlamaCpp";

        // Power Management: Auto No AI on Battery Power
        public bool DisableAiOnBattery { get; set; } = false;

        public bool IsExplicitAiDisabled => string.Equals(AiBackend, "None", StringComparison.OrdinalIgnoreCase) || 
                                            string.Equals(AiBackend, "Disabled", StringComparison.OrdinalIgnoreCase);

        public bool IsBatterySaverActive => DisableAiOnBattery && IsRunningOnBattery();

        public bool IsAiDisabled => IsExplicitAiDisabled || IsBatterySaverActive;

        public static bool IsRunningOnBattery()
        {
            try
            {
                return SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
            }
            catch
            {
                return false;
            }
        }

        // 1. llama.cpp (Local GGUF) Settings
        public string LlamaModelPath { get; set; } = "";
        public int LlamaServerPort { get; set; } = 8080;
        public string LlamaServerUrl { get; set; } = "http://127.0.0.1:8080/v1/chat/completions";
        public int LlamaGpuLayers { get; set; } = 99;
        public int LlamaContextSize { get; set; } = 8192;
        public bool AutoStartLlamaServer { get; set; } = true;
        public bool InstantVramUnload { get; set; } = false;

        // 2. Ollama (Local) Settings
        public string OllamaServerUrl { get; set; } = "http://127.0.0.1:11434/v1/chat/completions";
        public string OllamaModelName { get; set; } = "llama3.2";

        // 3. Cloud / Custom API Settings
        public string CloudApiUrl { get; set; } = "https://api.openai.com/v1/chat/completions";

        /// <summary>
        /// In-memory plaintext API key (never saved to disk in plain text).
        /// </summary>
        [JsonIgnore]
        public string CloudApiKey { get; set; } = "";

        /// <summary>
        /// DPAPI-encrypted API key string serialized securely into config.json.
        /// </summary>
        public string CloudApiKeyEncrypted
        {
            get => AccountCryptoService.EncryptString(CloudApiKey);
            set => CloudApiKey = AccountCryptoService.DecryptString(value);
        }

        public string CloudModelName { get; set; } = "gpt-4o-mini";

        // 4. Global LLM Inference Options
        public double Temperature { get; set; } = 0.2;
        public int MaxTokens { get; set; } = 350;

        /// <summary>
        /// Maximum character length of the email body sent to the AI model for summarization.
        /// 0 or negative = Unlimited (send entire email body).
        /// Default is 4000 characters (~1000 tokens). Minimum configurable is 500 characters.
        /// </summary>
        public int MaxSummaryEmailChars { get; set; } = 4000;

        // Email Fetching Options
        public int MaxEmailsPerAccount { get; set; } = 15;
        public bool OnlyUnread { get; set; } = false; // Fetch all inbox emails by default
        public bool MarkAsSeen { get; set; } = false;

        // Send Email Options
        public bool SaveSentEmailsToImap { get; set; } = true;

        // Attachment Download Options
        public string AttachmentDownloadPath { get; set; } = string.Empty;

        public string GetEffectiveAttachmentDownloadPath()
        {
            if (!string.IsNullOrWhiteSpace(AttachmentDownloadPath) && Directory.Exists(AttachmentDownloadPath))
            {
                return AttachmentDownloadPath;
            }
            string defaultDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(defaultDownloads)) return defaultDownloads;
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        // Multi-Selection Preview Display Option ("LastSelected" or "FirstSelected")
        public string MultiSelectPreview { get; set; } = "LastSelected";

        // UI & Layout Options
        public bool CollapseSidebarByDefault { get; set; } = false;
        public double WindowWidthScale { get; set; } = 0.60;
        public double WindowHeightScale { get; set; } = 0.56;
        public int WindowWidth { get; set; } = 0;
        public int WindowHeight { get; set; } = 0;

        // System Tray Daemon & Notification Options
        public bool AlwaysKeepOn { get; set; } = true;
        public bool EnableTrayNotifications { get; set; } = true;
        public int TrayRefreshIntervalMinutes { get; set; } = 5;
        public bool StartWithWindows { get; set; } = false;

        // System Prompt for AI Summarizer
        public string SystemPrompt { get; set; } = 
            "You are an executive assistant analyzing and summarizing incoming emails for the user.\r\n" +
            "Rules:\r\n" +
            "1. Priority Ranking: Assign an urgency/importance rank from 1 to 3:\r\n" +
            "   * Priority 2 (Normal - DEFAULT for most emails): Standard work correspondence, routine PR reviews, questions, meeting invites, project updates, invoices, personal messages, and general communications. If an email is not a critical emergency or bulk promotional digest, it is Priority 2.\r\n" +
            "   * Priority 1 (High / Urgent ONLY): Severe emergencies, production outages, broken CI/CD builds, critical security failures, immediate same-day deadlines, or urgent crisis escalations. Do NOT assign Priority 1 to routine requests, questions, or normal work tasks.\r\n" +
            "   * Priority 3 (Low / Newsletters / Bulk): Marketing promos, sales discounts, newsletters, market digests, trading signals, bulk announcements, and automated notifications with no personal action required.\r\n" +
            "   * Calibration: When in doubt between Priority 1 and Priority 2, ALWAYS assign Priority 2.\r\n" +
            "2. Summary: Write a concise 1-3 sentence executive brief in an objective, neutral third-person perspective. Accurately state key facts, errors, or required actions. Never use first-person pronouns.\r\n" +
            "3. Format: Return ONLY the summary and priority lines. Do NOT include scratchpad notes, numbered analysis steps, or markdown headers:\r\n" +
            "Summary: <1-3 sentence brief>\r\n" +
            "Priority: <1, 2, or 3>\r\n" +
            "4. Reasoning Models: If using a reasoning/thinking model (e.g. DeepSeek-R1, Qwen reasoning), keep internal analysis concise (under 150 words) before returning the summary and priority.";

        public static AppSettings CreateDefault()
        {
            return new AppSettings();
        }

        public string GetEffectiveEndpointUrl()
        {
            if (string.Equals(AiBackend, "Ollama", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(OllamaServerUrl) ? "http://127.0.0.1:11434/v1/chat/completions" : OllamaServerUrl.Trim();
            }
            if (string.Equals(AiBackend, "Cloud", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(CloudApiUrl) ? "https://api.openai.com/v1/chat/completions" : CloudApiUrl.Trim();
            }
            return string.IsNullOrWhiteSpace(LlamaServerUrl) ? "http://127.0.0.1:8080/v1/chat/completions" : LlamaServerUrl.Trim();
        }

        public string GetEffectiveModelName()
        {
            if (string.Equals(AiBackend, "Ollama", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(OllamaModelName) ? "llama3.2" : OllamaModelName.Trim();
            }
            if (string.Equals(AiBackend, "Cloud", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(CloudModelName) ? "gpt-4o-mini" : CloudModelName.Trim();
            }
            if (!string.IsNullOrWhiteSpace(LlamaModelPath))
            {
                return Path.GetFileName(LlamaModelPath);
            }
            return "llama.cpp";
        }

        public string GetBackendDisplayName()
        {
            if (IsBatterySaverActive)
            {
                return $"No AI (Battery Saver - {GetConfiguredBackendDisplayName()})";
            }
            if (IsExplicitAiDisabled)
            {
                return "No AI (Disabled)";
            }
            return GetConfiguredBackendDisplayName();
        }

        public string GetConfiguredBackendDisplayName()
        {
            if (string.Equals(AiBackend, "Ollama", StringComparison.OrdinalIgnoreCase))
            {
                return $"Ollama ({GetEffectiveModelName()})";
            }
            if (string.Equals(AiBackend, "Cloud", StringComparison.OrdinalIgnoreCase))
            {
                return $"Cloud API ({GetEffectiveModelName()})";
            }
            if (IsExplicitAiDisabled)
            {
                return "No AI (Disabled)";
            }
            string modelName = string.IsNullOrWhiteSpace(LlamaModelPath) ? "Not Selected" : Path.GetFileName(LlamaModelPath);
            return $"llama.cpp ({modelName})";
        }

        public string? GetEffectiveApiKey()
        {
            if (string.Equals(AiBackend, "Cloud", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(CloudApiKey))
            {
                return CloudApiKey.Trim();
            }
            return null;
        }
    }
}
