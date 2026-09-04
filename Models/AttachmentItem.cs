using System;
using System.IO;

namespace KerkenezMail.Models
{
    public class AttachmentItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string MimeType { get; set; } = "application/octet-stream";

        public string FormattedSize
        {
            get
            {
                if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
                if (FileSizeBytes < 1024 * 1024) return $"{FileSizeBytes / 1024.0:F1} KB";
                return $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
            }
        }

        public static AttachmentItem FromFile(string filePath)
        {
            var fi = new FileInfo(filePath);
            return new AttachmentItem
            {
                FilePath = filePath,
                FileName = fi.Name,
                FileSizeBytes = fi.Length,
                MimeType = MimeKit.MimeTypes.GetMimeType(filePath)
            };
        }
    }
}
