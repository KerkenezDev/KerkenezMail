using System;

namespace KerkenezMail.Models
{
    public enum MailFolderType
    {
        Inbox,
        Sent,
        Archive,
        Spam,
        Trash
    }

    public static class MailFolderExtensions
    {
        public static string GetDisplayName(this MailFolderType folder) => folder switch
        {
            MailFolderType.Inbox => "Inbox",
            MailFolderType.Sent => "Sent",
            MailFolderType.Archive => "Archived",
            MailFolderType.Spam => "Spam",
            MailFolderType.Trash => "Trash",
            _ => folder.ToString()
        };

        public static string GetIconGlyph(this MailFolderType folder) => folder switch
        {
            MailFolderType.Inbox => "\uE715",    // Mail (Inbox)
            MailFolderType.Sent => "\uE89C",     // Sent
            MailFolderType.Archive => "\uE7B8",  // Archive
            MailFolderType.Spam => "\uE7BA",     // Spam / Junk
            MailFolderType.Trash => "\uE74D",    // Trash bin
            _ => "\uE715"
        };
    }
}
