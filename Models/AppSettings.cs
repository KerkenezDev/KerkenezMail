using System.Collections.Generic;

namespace EmailSummarizer.Models
{
    public class AppSettings
    {
        public List<EmailAccount> Accounts { get; set; } = new List<EmailAccount>();

        // llama.cpp & LLM Server Settings
        public string LlamaModelPath { get; set; } = "";
        public int LlamaServerPort { get; set; } = 8080;
        public string LlamaServerUrl { get; set; } = "http://127.0.0.1:8080/v1/chat/completions";
        public int LlamaGpuLayers { get; set; } = 99;
        public bool AutoStartLlamaServer { get; set; } = true;
        
        // Keep model in memory while app is open, only unload on app close
        public bool InstantVramUnload { get; set; } = false;

        // Email Fetching Options
        public int MaxEmailsPerAccount { get; set; } = 15;
        public bool OnlyUnread { get; set; } = false; // Fetch all inbox emails by default
        public bool MarkAsSeen { get; set; } = false;

        // System Prompt for AI Summarizer
        public string SystemPrompt { get; set; } = 
            "You are an executive assistant summarizing incoming emails for the user.\r\n" +
            "Rules:\r\n" +
            "1. Perspective: Write in an objective, neutral third-person perspective (e.g., 'Tomorrow\\'s sync call...', 'The sender is notifying...'). NEVER use first-person pronouns like 'I', 'we', or 'our'.\r\n" +
            "2. Factual Accuracy: State ONLY facts directly mentioned in the text. Do not assume, extrapolate, or invent metrics, deadlines, or action items.\r\n" +
            "3. Length: For brief messages or quick updates, provide a single clear, concise sentence. For longer emails, provide 2-3 concise sentences highlighting core points and direct action items.\r\n" +
            "4. Clean Output: Return ONLY the summary text itself. Do NOT include greetings, preambles, quotes, or markdown headers.";

        public static AppSettings CreateDefault()
        {
            return new AppSettings();
        }
    }
}
