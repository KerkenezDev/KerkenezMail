using System.Collections.Generic;

namespace KerkenezMail.Models
{
    /// <summary>
    /// Represents a single user question and assistant answer turn within an in-memory Ask More session.
    /// </summary>
    public sealed class AskMoreTurn
    {
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public bool WasStopped { get; set; }
        public bool IsError { get; set; }
    }

    /// <summary>
    /// Ephemeral in-memory Ask More state for a specific email (never persisted to disk).
    /// </summary>
    public sealed class AskMoreSession
    {
        public string EmailKey { get; init; } = string.Empty;
        public MailFolderType Folder { get; init; } = MailFolderType.Inbox;
        public bool IsChatMode { get; set; }
        public string DraftInput { get; set; } = string.Empty;
        public List<AskMoreTurn> Turns { get; } = new List<AskMoreTurn>();

        public static string BuildKey(EmailItem email)
        {
            if (email.UniqueId > 0)
            {
                return $"{email.AccountEmail}|{email.Folder}|{email.UniqueId}";
            }
            return email.Id;
        }
    }
}
