namespace KerkenezMail.Languages
{
    /// <summary>
    /// Strongly-typed string keys for all localized user interface elements in Kerkenez Mail.
    /// Grouped logically by application section and feature.
    /// </summary>
    public static class StringKeys
    {
        // ==========================================
        // 1. Navigation & Sidebar
        // ==========================================
        public const string NavInbox = "Nav.Inbox";
        public const string NavSendMail = "Nav.SendMail";
        public const string NavAccounts = "Nav.Accounts";
        public const string NavSettings = "Nav.Settings";
        public const string NavLiveLogs = "Nav.LiveLogs";
        public const string NavSent = "Nav.Sent";
        public const string NavArchived = "Nav.Archived";
        public const string NavSpam = "Nav.Spam";
        public const string NavTrash = "Nav.Trash";
        public const string NavLiveImap = "Nav.LiveImap";
        public const string NavLiveImapActive = "Nav.LiveImapActive";
        public const string NavLiveImapOff = "Nav.LiveImapOff";
        public const string NavTipExpandSidebar = "Nav.TipExpandSidebar";
        public const string NavTipCollapseSidebar = "Nav.TipCollapseSidebar";
        public const string NavTipExpandFolders = "Nav.TipExpandFolders";
        public const string NavTipCollapseFolders = "Nav.TipCollapseFolders";

        // ==========================================
        // 2. Main Window & Shell
        // ==========================================
        public const string AppTitle = "Shell.AppTitle";
        public const string MainShortcutsPromptTitle = "Main.ShortcutsPromptTitle";
        public const string MainShortcutsPromptDesc = "Main.ShortcutsPromptDesc";
        public const string TrayOpen = "Shell.TrayOpen";
        public const string TrayLiveImapActive = "Shell.TrayLiveImapActive";
        public const string TrayLiveImapOff = "Shell.TrayLiveImapOff";
        public const string TrayCheckNow = "Shell.TrayCheckNow";
        public const string TraySettings = "Shell.TraySettings";
        public const string TrayExit = "Shell.TrayExit";

        // ==========================================
        // 3. Inbox & Summaries View
        // ==========================================
        public const string InboxRefresh = "Inbox.Refresh";
        public const string InboxCopySummary = "Inbox.CopySummary";
        public const string InboxExport = "Inbox.Export";
        public const string InboxOpenInBrowser = "Inbox.OpenInBrowser";
        public const string InboxTipRefresh = "Inbox.TipRefresh";
        public const string InboxTipCopySummary = "Inbox.TipCopySummary";
        public const string InboxTipExport = "Inbox.TipExport";
        public const string InboxTipOpenInBrowser = "Inbox.TipOpenInBrowser";
        public const string InboxTipArchive = "Inbox.TipArchive";
        public const string InboxTipDelete = "Inbox.TipDelete";
        public const string InboxTipReply = "Inbox.TipReply";
        public const string InboxTipMoveToInbox = "Inbox.TipMoveToInbox";
        public const string InboxAccountLabel = "Inbox.AccountLabel";
        public const string InboxAllAccounts = "Inbox.AllAccounts";
        public const string InboxListHeader = "Inbox.ListHeader";
        public const string InboxListHeaderUnread = "Inbox.ListHeaderUnread";
        public const string InboxSubjectPrefix = "Inbox.SubjectPrefix";
        public const string InboxNoEmailSelected = "Inbox.NoEmailSelected";
        public const string InboxAiExecutiveSummary = "Inbox.AiExecutiveSummary";
        public const string InboxAiGeneratedVram = "Inbox.AiGeneratedVram";
        public const string InboxAiSummaryPlaceholder = "Inbox.AiSummaryPlaceholder";
        public const string InboxAttachmentsTitle = "Inbox.AttachmentsTitle";
        public const string InboxTagRead = "Inbox.TagRead";
        public const string InboxTagUnread = "Inbox.TagUnread";
        public const string InboxTagArchived = "Inbox.TagArchived";
        public const string InboxSummarize = "Inbox.Summarize";
        public const string InboxSearchPlaceholder = "Inbox.SearchPlaceholder";
        public const string InboxColAccount = "Inbox.ColAccount";
        public const string InboxColFrom = "Inbox.ColFrom";
        public const string InboxColSubject = "Inbox.ColSubject";
        public const string InboxColDate = "Inbox.ColDate";
        public const string InboxColPriority = "Inbox.ColPriority";
        public const string InboxPriorityHigh = "Inbox.PriorityHigh";
        public const string InboxPriorityNormal = "Inbox.PriorityNormal";
        public const string InboxPriorityLow = "Inbox.PriorityLow";
        public const string InboxEmptyTitle = "Inbox.EmptyTitle";
        public const string InboxEmptySubtitle = "Inbox.EmptySubtitle";
        public const string InboxNoSelectionTitle = "Inbox.NoSelectionTitle";
        public const string InboxNoSelectionSubtitle = "Inbox.NoSelectionSubtitle";
        public const string InboxDetailFrom = "Inbox.DetailFrom";
        public const string InboxDetailTo = "Inbox.DetailTo";
        public const string InboxDetailDate = "Inbox.DetailDate";
        public const string InboxDetailAccount = "Inbox.DetailAccount";
        public const string InboxTabSummary = "Inbox.TabSummary";
        public const string InboxTabOriginal = "Inbox.TabOriginal";
        public const string InboxBtnMarkRead = "Inbox.BtnMarkRead";
        public const string InboxBtnMarkUnread = "Inbox.BtnMarkUnread";
        public const string InboxBtnDelete = "Inbox.BtnDelete";
        public const string InboxBtnMoveInbox = "Inbox.BtnMoveInbox";
        public const string InboxBtnReply = "Inbox.BtnReply";
        public const string InboxBtnForward = "Inbox.BtnForward";
        public const string InboxBtnDownloadAttachments = "Inbox.BtnDownloadAttachments";
        public const string InboxLoadingStatus = "Inbox.LoadingStatus";
        public const string InboxSummarizingStatus = "Inbox.SummarizingStatus";
        public const string InboxLiveActiveBadge = "Inbox.LiveActiveBadge";
        public const string InboxMultiSelectCount = "Inbox.MultiSelectCount";
        public const string InboxSavedAttachmentsToast = "Inbox.SavedAttachmentsToast";
        public const string InboxSavedAttachmentsStatus = "Inbox.SavedAttachmentsStatus";
        public const string InboxDownloadComplete = "Inbox.DownloadComplete";
        public const string InboxDownloadError = "Inbox.DownloadError";
        public const string InboxSummaryCopiedToast = "Inbox.SummaryCopiedToast";
        public const string InboxAllSummariesCopiedToast = "Inbox.AllSummariesCopiedToast";
        public const string InboxNoSummaryToCopy = "Inbox.NoSummaryToCopy";
        public const string InboxNoEmailsToExport = "Inbox.NoEmailsToExport";
        public const string InboxExportSuccessToast = "Inbox.ExportSuccessToast";
        public const string InboxExportSuccessTitle = "Inbox.ExportSuccessTitle";
        public const string InboxExportErrorToast = "Inbox.ExportErrorToast";
        public const string InboxExportErrorTitle = "Inbox.ExportErrorTitle";
        public const string InboxBrowserErrorToast = "Inbox.BrowserErrorToast";
        public const string InboxBrowserErrorTitle = "Inbox.BrowserErrorTitle";
        public const string InboxAccountNotFound = "Inbox.AccountNotFound";
        public const string InboxDeletePermanentlyTip = "Inbox.DeletePermanentlyTip";
        public const string InboxDeleteMoveTip = "Inbox.DeleteMoveTip";
        public const string InboxDownloadedSingleToast = "Inbox.DownloadedSingleToast";
        public const string InboxDownloadedSingleStatus = "Inbox.DownloadedSingleStatus";
        public const string InboxDownloadFailedStatus = "Inbox.DownloadFailedStatus";
        public const string InboxDownloadFailed = "Inbox.DownloadFailed";

        // ==========================================
        // 4. Send Mail / Compose
        // ==========================================
        public const string SendTitle = "Send.Title";
        public const string SendThreadedReply = "Send.ThreadedReply";
        public const string SendBackToInbox = "Send.BackToInbox";
        public const string SendPopOut = "Send.PopOut";
        public const string SendFrom = "Send.From";
        public const string SendCcBccToggle = "Send.CcBccToggle";
        public const string SendTo = "Send.To";
        public const string SendCc = "Send.Cc";
        public const string SendBcc = "Send.Bcc";
        public const string SendSubject = "Send.Subject";
        public const string SendToPlaceholder = "Send.ToPlaceholder";
        public const string SendCcPlaceholder = "Send.CcPlaceholder";
        public const string SendBccPlaceholder = "Send.BccPlaceholder";
        public const string SendSubjectPlaceholder = "Send.SubjectPlaceholder";
        public const string SendBodyPlaceholder = "Send.BodyPlaceholder";
        public const string SendAttachments = "Send.Attachments";
        public const string SendAddAttachment = "Send.AddAttachment";
        public const string SendRemoveAttachment = "Send.RemoveAttachment";
        public const string SendBtnSend = "Send.BtnSend";
        public const string SendBtnDiscard = "Send.BtnDiscard";
        public const string SendBtnSaveDraft = "Send.BtnSaveDraft";
        public const string SendSending = "Send.Sending";
        public const string SendSuccess = "Send.Success";
        public const string SendError = "Send.Error";
        public const string SendAiAssist = "Send.AiAssist";
        public const string SendDraftsTitle = "Send.DraftsTitle";
        public const string SendTabMarkdown = "Send.TabMarkdown";
        public const string SendTabPlaintext = "Send.TabPlaintext";
        public const string SendTabHtml = "Send.TabHtml";
        public const string SendFormatMultipart = "Send.FormatMultipart";
        public const string SendFormatPlaintext = "Send.FormatPlaintext";
        public const string SendDropHint = "Send.DropHint";
        public const string SendBrowseFiles = "Send.BrowseFiles";
        public const string SendAttachmentSummary = "Send.AttachmentSummary";
        public const string SendStatusHint = "Send.StatusHint";
        public const string SendConfirmDiscard = "Send.ConfirmDiscard";
        public const string SendDiscardTitle = "Send.DiscardTitle";
        public const string SendToolbarBold = "Send.ToolbarBold";
        public const string SendToolbarItalic = "Send.ToolbarItalic";
        public const string SendToolbarHeader = "Send.ToolbarHeader";
        public const string SendToolbarLink = "Send.ToolbarLink";
        public const string SendToolbarBulletList = "Send.ToolbarBulletList";
        public const string SendToolbarNumberedList = "Send.ToolbarNumberedList";
        public const string SendToolbarQuote = "Send.ToolbarQuote";
        public const string SendToolbarCode = "Send.ToolbarCode";
        public const string SendToolbarRule = "Send.ToolbarRule";
        public const string SendMissingRecipient = "Send.MissingRecipient";
        public const string SendMissingRecipientTitle = "Send.MissingRecipientTitle";
        public const string SendMissingAccount = "Send.MissingAccount";
        public const string SendMissingAccountTitle = "Send.MissingAccountTitle";
        public const string SendNoSubjectPrompt = "Send.NoSubjectPrompt";
        public const string SendNoSubjectTitle = "Send.NoSubjectTitle";
        public const string SendSentTitle = "Send.SentTitle";
        public const string SendFailedTitle = "Send.FailedTitle";

        // ==========================================
        // 5. Accounts View & Dialog
        // ==========================================
        public const string AccountsTitle = "Accounts.Title";
        public const string AccountsSubtitle = "Accounts.Subtitle";
        public const string AccountsBtnAdd = "Accounts.BtnAdd";
        public const string AccountsBtnEdit = "Accounts.BtnEdit";
        public const string AccountsBtnDelete = "Accounts.BtnDelete";
        public const string AccountsBtnTest = "Accounts.BtnTest";
        public const string AccountsColEmail = "Accounts.ColEmail";
        public const string AccountsColProvider = "Accounts.ColProvider";
        public const string AccountsColServer = "Accounts.ColServer";
        public const string AccountsColStatus = "Accounts.ColStatus";
        public const string AccountsStatusUntested = "Accounts.StatusUntested";
        public const string AccountsStatusConnected = "Accounts.StatusConnected";
        public const string AccountsStatusConnectedUnread = "Accounts.StatusConnectedUnread";
        public const string AccountsStatusFailed = "Accounts.StatusFailed";
        public const string AccountsStatusError = "Accounts.StatusError";
        public const string AccountsEmptyDesc = "Accounts.EmptyDesc";
        public const string AccountsDeleteConfirm = "Accounts.DeleteConfirm";

        public const string AddAccTitle = "AddAcc.Title";
        public const string AddAccEditTitle = "AddAcc.EditTitle";
        public const string AddAccProviderPreset = "AddAcc.ProviderPreset";
        public const string AddAccEmail = "AddAcc.Email";
        public const string AddAccPassword = "AddAcc.Password";
        public const string AddAccDisplayName = "AddAcc.DisplayName";
        public const string AddAccImapServer = "AddAcc.ImapServer";
        public const string AddAccImapPort = "AddAcc.ImapPort";
        public const string AddAccImapSsl = "AddAcc.ImapSsl";
        public const string AddAccSmtpServer = "AddAcc.SmtpServer";
        public const string AddAccSmtpPort = "AddAcc.SmtpPort";
        public const string AddAccSmtpSsl = "AddAcc.SmtpSsl";
        public const string AddAccOAuthSignIn = "AddAcc.OAuthSignIn";
        public const string AddAccOAuthSuccess = "AddAcc.OAuthSuccess";
        public const string AddAccOAuthClickHelp = "AddAcc.OAuthClickHelp";
        public const string AddAccHelpGmail = "AddAcc.HelpGmail";
        public const string AddAccTestConnection = "AddAcc.TestConnection";
        public const string AddAccTesting = "AddAcc.Testing";
        public const string AddAccSave = "AddAcc.Save";
        public const string AddAccCancel = "AddAcc.Cancel";

        // ==========================================
        // 6. Settings View
        // ==========================================
        public const string SettingsTitle = "Settings.Title";
        public const string SettingsSecAiBackend = "Settings.SecAiBackend";
        public const string SettingsBackendSelect = "Settings.BackendSelect";
        public const string SettingsBackendLlama = "Settings.BackendLlama";
        public const string SettingsBackendOllama = "Settings.BackendOllama";
        public const string SettingsBackendCloud = "Settings.BackendCloud";
        public const string SettingsBackendNoAi = "Settings.BackendNoAi";
        public const string SettingsBatteryWarningTitle = "Settings.BatteryWarningTitle";
        public const string SettingsBatteryWarningDesc = "Settings.BatteryWarningDesc";
        public const string SettingsLlamaModelPath = "Settings.LlamaModelPath";
        public const string SettingsLlamaLayers = "Settings.LlamaLayers";
        public const string SettingsLlamaPort = "Settings.LlamaPort";
        public const string SettingsLlamaContext = "Settings.LlamaContext";
        public const string SettingsLlamaUrl = "Settings.LlamaUrl";
        public const string SettingsLlamaAutoStart = "Settings.LlamaAutoStart";
        public const string SettingsLlamaInstantVram = "Settings.LlamaInstantVram";
        public const string SettingsOllamaInfo = "Settings.OllamaInfo";
        public const string SettingsOllamaUrl = "Settings.OllamaUrl";
        public const string SettingsOllamaModel = "Settings.OllamaModel";
        public const string SettingsSuggestions = "Settings.Suggestions";
        public const string SettingsCloudPreset = "Settings.CloudPreset";
        public const string SettingsCloudPresetSelect = "Settings.CloudPresetSelect";
        public const string SettingsCloudUrl = "Settings.CloudUrl";
        public const string SettingsCloudKey = "Settings.CloudKey";
        public const string SettingsCloudShow = "Settings.CloudShow";
        public const string SettingsCloudHide = "Settings.CloudHide";
        public const string SettingsCloudModel = "Settings.CloudModel";
        public const string SettingsNoAiTitle = "Settings.NoAiTitle";
        public const string SettingsNoAiDisclaimer = "Settings.NoAiDisclaimer";
        public const string SettingsTemp = "Settings.Temp";
        public const string SettingsTempDesc = "Settings.TempDesc";
        public const string SettingsMaxTokens = "Settings.MaxTokens";
        public const string SettingsMaxTokensDesc = "Settings.MaxTokensDesc";
        public const string SettingsTokenTip = "Settings.TokenTip";
        public const string SettingsEmailLimitHeader = "Settings.EmailLimitHeader";
        public const string SettingsEmailLimitDesc = "Settings.EmailLimitDesc";
        public const string SettingsUnlimited = "Settings.Unlimited";
        public const string SettingsPresets = "Settings.Presets";
        public const string SettingsCharsDefault = "Settings.CharsDefault";
        public const string SettingsChars8k = "Settings.Chars8k";
        public const string SettingsChars16k = "Settings.Chars16k";
        public const string SettingsChars32k = "Settings.Chars32k";
        public const string SettingsCharsUnlimited = "Settings.CharsUnlimited";
        public const string SettingsSecBattery = "Settings.SecBattery";
        public const string SettingsBatteryDisableAi = "Settings.BatteryDisableAi";
        public const string SettingsBatteryDesc = "Settings.BatteryDesc";
        public const string SettingsBatteryActive = "Settings.BatteryActive";
        public const string SettingsBatteryAc = "Settings.BatteryAc";
        public const string SettingsSecLanguage = "Settings.SecLanguage";
        public const string SettingsLanguageLabel = "Settings.LanguageLabel";
        public const string SettingsLanguageDesc = "Settings.LanguageDesc";
        public const string SettingsSecEmail = "Settings.SecEmail";
        public const string SettingsMaxEmails = "Settings.MaxEmails";
        public const string SettingsOnlyUnread = "Settings.OnlyUnread";
        public const string SettingsMarkAsSeen = "Settings.MarkAsSeen";
        public const string SettingsMultiSelectPreview = "Settings.MultiSelectPreview";
        public const string SettingsMultiSelectLast = "Settings.MultiSelectLast";
        public const string SettingsMultiSelectFirst = "Settings.MultiSelectFirst";
        public const string SettingsSecAttachments = "Settings.SecAttachments";
        public const string SettingsDownloadPathHeader = "Settings.DownloadPathHeader";
        public const string SettingsDownloadPathDesc = "Settings.DownloadPathDesc";
        public const string SettingsDownloadPathSelectDesc = "Settings.DownloadPathSelectDesc";
        public const string SettingsBrowse = "Settings.Browse";
        public const string SettingsDefault = "Settings.Default";
        public const string SettingsSecUi = "Settings.SecUi";
        public const string SettingsCollapseSidebar = "Settings.CollapseSidebar";
        public const string SettingsScalingHeader = "Settings.ScalingHeader";
        public const string SettingsScalingDesc = "Settings.ScalingDesc";
        public const string SettingsWidthScale = "Settings.WidthScale";
        public const string SettingsHeightScale = "Settings.HeightScale";
        public const string SettingsResizeActive = "Settings.ResizeActive";
        public const string SettingsPresetDefault = "Settings.PresetDefault";
        public const string SettingsPresetCompact = "Settings.PresetCompact";
        public const string SettingsPresetLarge = "Settings.PresetLarge";
        public const string SettingsPresetMax = "Settings.PresetMax";
        public const string SettingsLaunchDimensions = "Settings.LaunchDimensions";
        public const string SettingsAddShortcuts = "Settings.AddShortcuts";
        public const string SettingsShortcutsSuccess = "Settings.ShortcutsSuccess";
        public const string SettingsShortcutsError = "Settings.ShortcutsError";
        public const string SettingsSecTray = "Settings.SecTray";
        public const string SettingsAlwaysKeepOn = "Settings.AlwaysKeepOn";
        public const string SettingsEnableTrayNotifs = "Settings.EnableTrayNotifs";
        public const string SettingsCheckInterval = "Settings.CheckInterval";
        public const string SettingsStartWithWindows = "Settings.StartWithWindows";
        public const string SettingsRestartDaemon = "Settings.RestartDaemon";
        public const string SettingsSecPrompt = "Settings.SecPrompt";
        public const string SettingsBtnSave = "Settings.BtnSave";
        public const string SettingsBtnReset = "Settings.BtnReset";
        public const string SettingsBtnTestLlm = "Settings.BtnTestLlm";
        public const string SettingsSavedToast = "Settings.SavedToast";
        public const string SettingsResetConfirm = "Settings.ResetConfirm";

        // ==========================================
        // 7. Live Logs View
        // ==========================================
        public const string LogsTitle = "Logs.Title";
        public const string LogsSubtitle = "Logs.Subtitle";
        public const string LogsBtnCopy = "Logs.BtnCopy";
        public const string LogsBtnClear = "Logs.BtnClear";
        public const string LogsBtnExport = "Logs.BtnExport";
        public const string LogsChkAutoScroll = "Logs.ChkAutoScroll";
        public const string LogsCopiedMsg = "Logs.CopiedMsg";

        // ==========================================
        // 8. Common & Dialog Messages
        // ==========================================
        public const string CommonSave = "Common.Save";
        public const string CommonCancel = "Common.Cancel";
        public const string CommonClose = "Common.Close";
        public const string CommonDelete = "Common.Delete";
        public const string CommonOk = "Common.Ok";
        public const string CommonYes = "Common.Yes";
        public const string CommonNo = "Common.No";
        public const string CommonError = "Common.Error";
        public const string CommonWarning = "Common.Warning";
        public const string CommonSuccess = "Common.Success";
        public const string CommonLoading = "Common.Loading";
        public const string CommonConnecting = "Common.Connecting";

        // ==========================================
        // 9. Status Bar & Metrics
        // ==========================================
        public const string StatusSyncComplete = "Status.SyncComplete";
        public const string StatusReady = "Status.Ready";
        public const string StatusReadyEmails = "Status.ReadyEmails";
        public const string StatusReadyFolder = "Status.ReadyFolder";
        public const string StatusAccountsCount = "Status.AccountsCount";
        public const string StatusAccountsBackend = "Status.AccountsBackend";
        public const string StatusReadyBackend = "Status.ReadyBackend";
        public const string StatusVramFree = "Status.VramFree";
        public const string StatusModelLoaded = "Status.ModelLoaded";
        public const string StatusOllamaActive = "Status.OllamaActive";
        public const string StatusCloudActive = "Status.CloudActive";
        public const string StatusAiDisabled = "Status.AiDisabled";
        public const string StatusBatterySaverNoAi = "Status.BatterySaverNoAi";
        public const string StatusOnDemandVram = "Status.OnDemandVram";
        public const string StatusSummaryReady = "Status.SummaryReady";
        public const string StatusSyncingFolder = "Status.SyncingFolder";
        public const string StatusNoAccounts = "Status.NoAccounts";
        public const string StatusLiveConnecting = "Status.LiveConnecting";
        public const string StatusLiveListening = "Status.LiveListening";
        public const string StatusLiveStopped = "Status.LiveStopped";
        public const string StatusLiveDone = "Status.LiveDone";
        public const string StatusLiveNewEmail = "Status.LiveNewEmail";
        public const string StatusStartingUp = "Status.StartingUp";
        public const string StatusDisabledClassic = "Status.DisabledClassic";
        public const string StatusModelLoadFailed = "Status.ModelLoadFailed";
    }
}
