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
