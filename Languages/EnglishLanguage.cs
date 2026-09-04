namespace KerkenezMail.Languages
{
    /// <summary>
    /// Master English (en) language definition for Kerkenez Mail.
    /// Acts as the default fallback language across the entire application.
    /// </summary>
    public class EnglishLanguage : BaseLanguage
    {
        public override string Code => "en";
        public override string Name => "English";
        public override string EnglishName => "English";
        public override string FlagEmoji => "🇬🇧";

        protected override void InitTranslations()
        {
            // ==============================================================================
            // KEY                                       TRANSLATION
            // ==============================================================================

            // --- 1. Navigation & Sidebar ---
            Set(StringKeys.NavInbox,                     "Inbox");
            Set(StringKeys.NavSendMail,                  "Send Mail");
            Set(StringKeys.NavAccounts,                  "Accounts");
            Set(StringKeys.NavSettings,                  "Settings");
            Set(StringKeys.NavLiveLogs,                  "Live Logs");
            Set(StringKeys.NavSent,                      "Sent");
            Set(StringKeys.NavArchived,                  "Archived");
            Set(StringKeys.NavSpam,                      "Spam");
            Set(StringKeys.NavTrash,                     "Trash");
            Set(StringKeys.NavLiveImap,                  "Live IMAP");
            Set(StringKeys.NavLiveImapActive,            "Live IMAP is active (Click to turn off)");
            Set(StringKeys.NavLiveImapOff,               "Live IMAP is off (Click to turn on)");
            Set(StringKeys.NavTipExpandSidebar,          "Expand sidebar (Ctrl+B)");
            Set(StringKeys.NavTipCollapseSidebar,        "Collapse sidebar (Ctrl+B)");
            Set(StringKeys.NavTipExpandFolders,          "Expand mail folders (Sent, Archive, Trash...)");
            Set(StringKeys.NavTipCollapseFolders,        "Collapse mail folders");

            // --- 2. Main Window & Shell ---
            Set(StringKeys.AppTitle,                     "Kerkenez Mail (Win32)");
            Set(StringKeys.TrayOpen,                     "📬  Open Kerkenez Mail");
            Set(StringKeys.TrayLiveImapActive,           "⚡  Live IMAP: Active (Click to toggle)");
            Set(StringKeys.TrayLiveImapOff,              "💤  Live IMAP: Idle (Click to toggle)");
            Set(StringKeys.TrayCheckNow,                 "🔄  Check Now");
            Set(StringKeys.TraySettings,                 "⚙️  Settings");
            Set(StringKeys.TrayExit,                     "❌  Exit");

            // --- 3. Inbox & Summaries View ---
            Set(StringKeys.InboxRefresh,                 "Refresh");
            Set(StringKeys.InboxSummarize,               "AI Summarize");
            Set(StringKeys.InboxSearchPlaceholder,       "Search emails by sender, subject, or content...");
            Set(StringKeys.InboxColAccount,              "Account");
            Set(StringKeys.InboxColFrom,                 "From");
            Set(StringKeys.InboxColSubject,              "Subject");
            Set(StringKeys.InboxColDate,                 "Date");
            Set(StringKeys.InboxColPriority,             "Priority");
            Set(StringKeys.InboxPriorityHigh,            "HIGH");
            Set(StringKeys.InboxPriorityNormal,          "NORMAL");
            Set(StringKeys.InboxPriorityLow,             "LOW");
            Set(StringKeys.InboxEmptyTitle,              "No emails found");
            Set(StringKeys.InboxEmptySubtitle,           "Select an account or click Refresh to fetch emails.");
            Set(StringKeys.InboxNoSelectionTitle,        "No email selected");
            Set(StringKeys.InboxNoSelectionSubtitle,     "Choose an email from the list to view its summary and details.");
            Set(StringKeys.InboxDetailFrom,              "From:");
            Set(StringKeys.InboxDetailTo,                "To:");
            Set(StringKeys.InboxDetailDate,              "Date:");
            Set(StringKeys.InboxDetailAccount,           "Account:");
            Set(StringKeys.InboxTabSummary,              "AI Summary");
            Set(StringKeys.InboxTabOriginal,             "Original Email");
            Set(StringKeys.InboxBtnMarkRead,             "Mark Read");
            Set(StringKeys.InboxBtnMarkUnread,           "Mark Unread");
            Set(StringKeys.InboxBtnDelete,               "Delete");
            Set(StringKeys.InboxBtnMoveInbox,            "Move to Inbox");
            Set(StringKeys.InboxBtnReply,                "Reply");
            Set(StringKeys.InboxBtnForward,              "Forward");
            Set(StringKeys.InboxBtnDownloadAttachments,  "Download Attachments");
            Set(StringKeys.InboxLoadingStatus,           "Fetching emails...");
            Set(StringKeys.InboxSummarizingStatus,       "Summarizing with AI...");
            Set(StringKeys.InboxLiveActiveBadge,         "Live IMAP: Connected");
            Set(StringKeys.InboxMultiSelectCount,        "{0} emails selected");

            // --- 4. Send Mail / Compose ---
            Set(StringKeys.SendTitle,                    "Compose Email");
            Set(StringKeys.SendFrom,                     "From:");
            Set(StringKeys.SendTo,                       "To:");
            Set(StringKeys.SendCc,                       "Cc:");
            Set(StringKeys.SendBcc,                      "Bcc:");
            Set(StringKeys.SendSubject,                  "Subject:");
            Set(StringKeys.SendBodyPlaceholder,          "Write your email message here...");
            Set(StringKeys.SendAttachments,              "Attachments:");
            Set(StringKeys.SendAddAttachment,            "Attach File");
            Set(StringKeys.SendRemoveAttachment,         "Remove");
            Set(StringKeys.SendBtnSend,                  "Send Email");
            Set(StringKeys.SendBtnDiscard,               "Discard");
            Set(StringKeys.SendBtnSaveDraft,             "Save Draft");
            Set(StringKeys.SendSending,                  "Sending email...");
            Set(StringKeys.SendSuccess,                  "Email sent successfully!");
            Set(StringKeys.SendError,                    "Failed to send email.");
            Set(StringKeys.SendAiAssist,                 "AI Polish");
            Set(StringKeys.SendDraftsTitle,              "Drafts");

            // --- 5. Accounts View & Dialog ---
            Set(StringKeys.AccountsTitle,                "Email Accounts");
            Set(StringKeys.AccountsSubtitle,             "Manage your configured IMAP/SMTP accounts and credentials.");
            Set(StringKeys.AccountsBtnAdd,               "Add Account");
            Set(StringKeys.AccountsBtnEdit,              "Edit");
            Set(StringKeys.AccountsBtnDelete,            "Delete");
            Set(StringKeys.AccountsBtnTest,              "Test Connection");
            Set(StringKeys.AccountsColEmail,             "Email Address");
            Set(StringKeys.AccountsColProvider,          "Provider");
            Set(StringKeys.AccountsColServer,            "Incoming Server");
            Set(StringKeys.AccountsColStatus,            "Status");
            Set(StringKeys.AccountsStatusConnected,      "Connected");
            Set(StringKeys.AccountsStatusError,          "Connection Error");
            Set(StringKeys.AccountsDeleteConfirm,        "Are you sure you want to remove account '{0}'?");

            Set(StringKeys.AddAccTitle,                  "Add Email Account");
            Set(StringKeys.AddAccEditTitle,              "Edit Email Account");
            Set(StringKeys.AddAccProviderPreset,         "Provider Preset:");
            Set(StringKeys.AddAccEmail,                  "Email Address:");
            Set(StringKeys.AddAccPassword,               "Password / App Password:");
            Set(StringKeys.AddAccDisplayName,            "Display Name:");
            Set(StringKeys.AddAccImapServer,             "IMAP Server:");
            Set(StringKeys.AddAccImapPort,               "IMAP Port:");
            Set(StringKeys.AddAccImapSsl,                "Use SSL/TLS for IMAP");
            Set(StringKeys.AddAccSmtpServer,             "SMTP Server:");
            Set(StringKeys.AddAccSmtpPort,               "SMTP Port:");
            Set(StringKeys.AddAccSmtpSsl,                "Use SSL/TLS for SMTP");
            Set(StringKeys.AddAccOAuthSignIn,            "Sign in with Microsoft");
            Set(StringKeys.AddAccOAuthSuccess,           "Microsoft OAuth signed in successfully!");
            Set(StringKeys.AddAccTestConnection,         "Test Connection");
            Set(StringKeys.AddAccTesting,                "Testing connection...");
            Set(StringKeys.AddAccSave,                   "Save Account");
            Set(StringKeys.AddAccCancel,                 "Cancel");

            // --- 6. Settings View ---
            Set(StringKeys.SettingsTitle,                "Settings");
            Set(StringKeys.SettingsSecAiBackend,         "🤖  AI Summarization & Priority");
            Set(StringKeys.SettingsSecBattery,           "🔋  Power & Battery Saver");
            Set(StringKeys.SettingsSecEmail,             "📧  Email Retrieval");
            Set(StringKeys.SettingsSecUi,                "🖥️  Interface & Layout");
            Set(StringKeys.SettingsSecLanguage,          "🌐  Language & Region");
            Set(StringKeys.SettingsSecTray,              "🔔  Notifications & Background Tray");
            Set(StringKeys.SettingsSecPrompt,            "📝  AI Prompt Template");

            Set(StringKeys.SettingsLanguageLabel,        "Interface Language:");
            Set(StringKeys.SettingsLanguageDesc,         "Select the display language for the user interface. Changes apply immediately.");

            Set(StringKeys.SettingsBackendLlama,         "🦙  Local llama.cpp (Embedded GGUF)");
            Set(StringKeys.SettingsBackendOllama,        "🦙  Local Ollama (localhost:11434)");
            Set(StringKeys.SettingsBackendCloud,         "☁️  Cloud / Custom API (OpenAI, OpenRouter, Groq, DeepSeek)");
            Set(StringKeys.SettingsBackendNoAi,          "🚫  No AI (Disable Priority & Summarizing)");

            Set(StringKeys.SettingsBatteryDisableAi,     "Automatically disable AI summarization when running on battery power");
            Set(StringKeys.SettingsBatteryActiveWarning, "AI is currently suspended because this device is running on battery power.");

            Set(StringKeys.SettingsMaxEmails,            "Max emails to fetch per account:");
            Set(StringKeys.SettingsOnlyUnread,           "Fetch unread emails only");
            Set(StringKeys.SettingsMarkAsSeen,           "Automatically mark emails as read when fetched");
            Set(StringKeys.SettingsDownloadPath,         "Default attachment download directory:");
            Set(StringKeys.SettingsBrowse,               "Browse...");

            Set(StringKeys.SettingsCollapseSidebar,      "Start with left sidebar collapsed by default");
            Set(StringKeys.SettingsWindowWidth,          "Window Width Scale (%):");
            Set(StringKeys.SettingsWindowHeight,         "Window Height Scale (%):");
            Set(StringKeys.SettingsApplySize,            "Apply Window Size Now");

            Set(StringKeys.SettingsAlwaysKeepOn,         "Keep application running in background system tray");
            Set(StringKeys.SettingsEnableTrayNotifs,      "Show Windows notifications for new emails");
            Set(StringKeys.SettingsCheckInterval,        "Background check interval (minutes):");
            Set(StringKeys.SettingsStartWithWindows,     "Start background daemon automatically with Windows");
            Set(StringKeys.SettingsRestartDaemon,        "Restart Background Daemon");

            Set(StringKeys.SettingsBtnSave,              "Save Settings");
            Set(StringKeys.SettingsBtnTestLlm,           "Test AI Backend");
            Set(StringKeys.SettingsSavedToast,           "Settings saved successfully!");

            // --- 7. Live Logs View ---
            Set(StringKeys.LogsTitle,                    "Live System Logs");
            Set(StringKeys.LogsSubtitle,                 "Real-time diagnostic events, IMAP connections, and AI inference logs.");
            Set(StringKeys.LogsBtnClear,                 "Clear Logs");
            Set(StringKeys.LogsBtnExport,                "Export Logs...");
            Set(StringKeys.LogsChkAutoScroll,            "Auto-scroll to latest");

            // --- 8. Common & Dialog Messages ---
            Set(StringKeys.CommonSave,                   "Save");
            Set(StringKeys.CommonCancel,                 "Cancel");
            Set(StringKeys.CommonClose,                  "Close");
            Set(StringKeys.CommonDelete,                 "Delete");
            Set(StringKeys.CommonOk,                     "OK");
            Set(StringKeys.CommonYes,                    "Yes");
            Set(StringKeys.CommonNo,                     "No");
            Set(StringKeys.CommonError,                  "Error");
            Set(StringKeys.CommonWarning,                "Warning");
            Set(StringKeys.CommonSuccess,                "Success");
            Set(StringKeys.CommonLoading,                "Loading...");
            Set(StringKeys.CommonConnecting,             "Connecting...");
        }
    }
}
