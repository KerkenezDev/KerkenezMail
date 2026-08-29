using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using EmailSummarizer.Services;

namespace EmailSummarizer.Models
{
    public class AppSettings
    {
        public List<string> AccountIds { get; set; } = new List<string>();

        // AI Backend Selection ("LlamaCpp", "Ollama", "Cloud")
        public string AiBackend { get; set; } = "LlamaCpp";

        // 1. llama.cpp (Local GGUF) Settings
        public string LlamaModelPath { get; set; } = "";
        public int LlamaServerPort { get; set; } = 8080;
        public string LlamaServerUrl { get; set; } = "http://127.0.0.1:8080/v1/chat/completions";
        public int LlamaGpuLayers { get; set; } = 99;
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

        // Email Fetching Options
        public int MaxEmailsPerAccount { get; set; } = 15;
        public bool OnlyUnread { get; set; } = false; // Fetch all inbox emails by default
        public bool MarkAsSeen { get; set; } = false;

        // Multi-Selection Preview Display Option ("LastSelected" or "FirstSelected")
        public string MultiSelectPreview { get; set; } = "LastSelected";

        // UI & Layout Options
        public bool CollapseSidebarByDefault { get; set; } = false;
        public double WindowWidthScale { get; set; } = 0.60;
        public double WindowHeightScale { get; set; } = 0.56;

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
            "   1 = High / Urgent / Action required / Critical deadline / Important direct request\r\n" +
            "   2 = Normal / Medium priority / Informational update / General business correspondence\r\n" +
            "   3 = Low / Newsletter / Promotional / Marketing / Automated system notification\r\n" +
            "2. Summary: Write a concise 1-3 sentence executive brief in an objective, neutral third-person perspective. State ONLY facts directly mentioned in the email text. Never use first-person pronouns.\r\n" +
            "3. Format: Output strictly in this format:\r\n" +
            "Priority: [1/2/3]\r\n" +
            "Summary: [summary text]";

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
            if (string.Equals(AiBackend, "Ollama", StringComparison.OrdinalIgnoreCase))
            {
                return $"Ollama ({GetEffectiveModelName()})";
            }
            if (string.Equals(AiBackend, "Cloud", StringComparison.OrdinalIgnoreCase))
            {
                return $"Cloud API ({GetEffectiveModelName()})";
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
