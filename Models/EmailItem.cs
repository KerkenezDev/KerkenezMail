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
        public string DisplayBody { get; set; } = string.Empty;
        public string? DisplayRtf { get; set; }
        public string Summary { get; set; } = string.Empty;
        public SummaryState Status { get; set; } = SummaryState.Pending;
        public string? ErrorMessage { get; set; }
        
        // Track whether email was already read on IMAP server
        public bool IsRead { get; set; }

        // Track whether email is archived
        public bool IsArchived { get; set; }

        // Priority ranking (1 = High / Urgent, 2 = Normal / Medium, 3 = Low / Newsletter, null = unranked)
        public int? Priority { get; set; } = null;

        // Newsletter and mailing list detection flags
        public bool IsMailingList { get; set; }
        public bool HasNewsletterFooter { get; set; }

        // Extracted hyperlinks for hover tooltips and actions
        public List<EmailLink> ExtractedLinks { get; set; } = new List<EmailLink>();

        public string DateString => Date.LocalDateTime.ToString("dd/MM/yyyy HH:mm");
        public string ShortDateString => Date.LocalDateTime.ToString("dd/MM HH:mm");
    }

    public class EmailLink
    {
        public string Text { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}
