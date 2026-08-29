using System;

namespace EmailSummarizer.Models
{
    public enum SummaryState
    {
        Pending,
        Summarizing,
        Completed,
        Failed
    }

    public class EmailItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public uint UniqueId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string AccountEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public DateTimeOffset Date { get; set; }
        public string RawBody { get; set; } = string.Empty;
        public string CleanBody { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public SummaryState Status { get; set; } = SummaryState.Pending;
        public string? ErrorMessage { get; set; }
        
        // Track whether email was already read on IMAP server
        public bool IsRead { get; set; }

        // Track whether email is archived
        public bool IsArchived { get; set; }

        public string DateString => Date.LocalDateTime.ToString("dd/MM/yyyy HH:mm");
        public string ShortDateString => Date.LocalDateTime.ToString("dd/MM HH:mm");
    }
}
