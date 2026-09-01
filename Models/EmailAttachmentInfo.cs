using System;
using System.IO;

namespace EmailSummarizer.Models
{
    public class EmailAttachmentInfo
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string MimeType { get; set; } = "application/octet-stream";
        public string? ContentId { get; set; }
        public int PartIndex { get; set; }

        public string FormattedSize
        {
            get
            {
                if (FileSizeBytes <= 0) return "Unknown size";
                if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
                if (FileSizeBytes < 1024 * 1024) return $"{FileSizeBytes / 1024.0:F1} KB";
                return $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
            }
        }

        public string GetFileIcon()
        {
            string ext = Path.GetExtension(FileName).ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "📄",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" => "🖼️",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
                ".txt" or ".md" or ".log" => "📝",
                ".docx" or ".doc" or ".odt" => "📘",
                ".xlsx" or ".xls" or ".csv" => "📊",
                ".pptx" or ".ppt" => "📙",
                ".mp3" or ".wav" or ".ogg" or ".m4a" => "🎵",
                ".mp4" or ".avi" or ".mkv" or ".mov" => "🎬",
                ".exe" or ".msi" or ".bat" or ".cmd" or ".ps1" => "⚙️",
                _ => "📎"
            };
        }
    }
}
