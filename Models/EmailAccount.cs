using System;
using System.Text.Json.Serialization;

namespace EmailSummarizer.Models
{
    public class EmailAccount
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Gmail Account";
        public string Email { get; set; } = "";
        public string AppPassword { get; set; } = "";
        public string Host { get; set; } = "imap.gmail.com";
        public int Port { get; set; } = 993;
        public bool UseSsl { get; set; } = true;
        public bool IsEnabled { get; set; } = true;

        // Provider & OAuth 2.0 (e.g. Outlook / Office 365)
        public string Provider { get; set; } = "Custom";
        public string EncryptedRefreshToken { get; set; } = "";
        public string EncryptedAccessToken { get; set; } = "";
        public DateTime? AccessTokenExpiresUtc { get; set; }
        public DateTime? LastRefreshedUtc { get; set; }

        [JsonIgnore]
        public bool IsOutlookOAuth => string.Equals(Provider, "OutlookOAuth", StringComparison.OrdinalIgnoreCase);

        // SMTP Settings
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public bool SmtpUseSsl { get; set; } = false;

        public string GetEffectiveSmtpHost()
        {
            if (!string.IsNullOrWhiteSpace(SmtpHost)) return SmtpHost.Trim();
            if (Host.Contains("gmail", StringComparison.OrdinalIgnoreCase)) return "smtp.gmail.com";
            if (Host.Contains("yahoo", StringComparison.OrdinalIgnoreCase)) return "smtp.mail.yahoo.com";
            if (Host.Contains("mail.me.com", StringComparison.OrdinalIgnoreCase) || Host.Contains("icloud", StringComparison.OrdinalIgnoreCase)) return "smtp.mail.me.com";
            if (Host.Contains("office365", StringComparison.OrdinalIgnoreCase) || Host.Contains("outlook", StringComparison.OrdinalIgnoreCase) || IsOutlookOAuth) return "smtp.office365.com";
            if (Host.StartsWith("imap.", StringComparison.OrdinalIgnoreCase)) return "smtp." + Host.Substring(5);
            return Host;
        }

        public int GetEffectiveSmtpPort()
        {
            if (SmtpPort > 0) return SmtpPort;
            return 587;
        }

        [JsonIgnore]
        public string ConnectionStatus { get; set; } = "Untested";

        [JsonIgnore]
        public string? ConnectionError { get; set; }

        public string GetMaskedPassword()
        {
            if (string.IsNullOrEmpty(AppPassword)) return "(empty)";
            if (AppPassword.Length <= 4) return new string('•', AppPassword.Length);
            return AppPassword.Substring(0, 2) + new string('•', AppPassword.Length - 4) + AppPassword.Substring(AppPassword.Length - 2);
        }

        public override string ToString()
        {
            return $"{Name} ({Email})";
        }
    }
}
