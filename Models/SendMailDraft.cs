using System;
using System.Collections.Generic;

namespace KerkenezMail.Models
{
    public class SendMailDraft
    {
        public EmailAccount? FromAccount { get; set; }
        public string To { get; set; } = string.Empty;
        public string Cc { get; set; } = string.Empty;
        public string Bcc { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string BodyMarkdown { get; set; } = string.Empty;

        // RFC 5322 Threading Headers
        public string? InReplyTo { get; set; }
        public List<string> References { get; set; } = new List<string>();

        // Attachments
        public List<AttachmentItem> Attachments { get; set; } = new List<AttachmentItem>();

        // Formatting Flags
        public bool IsReply { get; set; }
        public bool SendAsPlaintextOnly { get; set; } = false;
    }
}
