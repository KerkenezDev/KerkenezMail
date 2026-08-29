using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using EmailSummarizer.Models;

namespace EmailSummarizer.Services
{
    public class ImapService
    {
        public async Task<(bool Success, string Message, int UnreadCount)> TestConnectionAsync(
            EmailAccount account,
            CancellationToken ct = default)
        {
            using var client = new ImapClient();
            try
            {
                client.Timeout = 10000;
                var sslOption = account.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

                await client.ConnectAsync(account.Host, account.Port, sslOption, ct);
                await client.AuthenticateAsync(account.Email, account.AppPassword, ct);

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly, ct);
                var unreadUids = await inbox.SearchAsync(SearchQuery.NotSeen, ct);

                await client.DisconnectAsync(true, ct);
                return (true, $"Success: {unreadUids.Count} unread email(s) found.", unreadUids.Count);
            }
            catch (Exception ex)
            {
                return (false, $"Connection Failed: {ex.Message}", 0);
            }
        }

        public async Task<List<EmailItem>> FetchEmailsFromAccountAsync(
            EmailAccount account,
            AppSettings settings,
            IProgress<string>? logger = null,
            Action<EmailItem>? onEmailFetched = null,
            CancellationToken ct = default)
        {
            var emails = new List<EmailItem>();
            if (!account.IsEnabled) return emails;

            using var client = new ImapClient();
            try
            {
                logger?.Report($"[*] Connecting to {account.Name} ({account.Email})...");
                client.Timeout = 15000;
                var sslOption = account.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

                await client.ConnectAsync(account.Host, account.Port, sslOption, ct);
                await client.AuthenticateAsync(account.Email, account.AppPassword, ct);

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadWrite, ct);

                // Fetch either unread or all messages
                var searchQuery = settings.OnlyUnread ? SearchQuery.NotSeen : SearchQuery.All;
                var uids = await inbox.SearchAsync(searchQuery, ct);

                if (uids.Count == 0)
                {
                    logger?.Report($"[✓] {account.Name}: Inbox is empty.");
                    await client.DisconnectAsync(true, ct);
                    return emails;
                }

                // Take latest N messages (highest UIDs)
                var targetUids = uids.Reverse().Take(settings.MaxEmailsPerAccount).ToList();
                logger?.Report($"[*] {account.Name}: Found {uids.Count} total messages. Fetching {targetUids.Count} recent emails...");

                // Batch fetch flags (Seen/Unseen) for all target UIDs
                var summaries = await inbox.FetchAsync(targetUids, MessageSummaryItems.Flags | MessageSummaryItems.UniqueId, ct);
                var flagDict = summaries.ToDictionary(s => s.UniqueId, s => s.Flags);

                // Stream full MIME messages progressively
                foreach (var uid in targetUids)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var message = await inbox.GetMessageAsync(uid, ct);

                        bool isRead = false;
                        if (flagDict.TryGetValue(uid, out var flags) && flags.HasValue)
                        {
                            isRead = flags.Value.HasFlag(MessageFlags.Seen);
                        }

                        string cleanBody = ExtractAndCleanBody(message);

                        var emailItem = new EmailItem
                        {
                            UniqueId = uid.Id,
                            AccountName = account.Name,
                            AccountEmail = account.Email,
                            Subject = string.IsNullOrWhiteSpace(message.Subject) ? "(No Subject)" : message.Subject,
                            Sender = message.From.Mailboxes.FirstOrDefault()?.ToString() ?? "(Unknown Sender)",
                            Date = message.Date,
                            RawBody = message.TextBody ?? message.HtmlBody ?? string.Empty,
                            CleanBody = cleanBody,
                            IsRead = isRead,
                            Status = SummaryState.Pending
                        };

                        emails.Add(emailItem);

                        // Stream immediately to UI in real-time!
                        onEmailFetched?.Invoke(emailItem);

                        if (settings.MarkAsSeen && !isRead)
                        {
                            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.Report($"[!] Failed to load message UID {uid}: {ex.Message}");
                    }
                }

                await client.DisconnectAsync(true, ct);
                logger?.Report($"[✓] {account.Name}: Streamed {emails.Count} email(s) successfully.");
            }
            catch (OperationCanceledException)
            {
                logger?.Report($"[!] {account.Name}: Fetch cancelled.");
            }
            catch (Exception ex)
            {
                logger?.Report($"[!] {account.Name} Error: {ex.Message}");
            }

            return emails;
        }

        public async Task<List<EmailItem>> FetchAllAccountsParallelAsync(
            IEnumerable<EmailAccount> accounts,
            AppSettings settings,
            IProgress<string>? logger = null,
            Action<EmailItem>? onEmailFetched = null,
            CancellationToken ct = default)
        {
            var tasks = accounts
                .Where(a => a.IsEnabled)
                .Select(acc => FetchEmailsFromAccountAsync(acc, settings, logger, onEmailFetched, ct));

            var results = await Task.WhenAll(tasks);
            var allEmails = results.SelectMany(r => r)
                .OrderByDescending(e => e.Date)
                .ToList();

            return allEmails;
        }

        public async Task<bool> DeleteEmailsAsync(
            EmailAccount account,
            IEnumerable<uint> uids,
            IProgress<string>? logger = null,
            CancellationToken ct = default)
        {
            var uidList = uids.Select(u => new UniqueId(u)).ToList();
            if (uidList.Count == 0 || !account.IsEnabled) return true;

            using var client = new ImapClient();
            try
            {
                logger?.Report($"[*] Connecting to {account.Name} to delete {uidList.Count} message(s)...");
                client.Timeout = 15000;
                var sslOption = account.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

                await client.ConnectAsync(account.Host, account.Port, sslOption, ct);
                await client.AuthenticateAsync(account.Email, account.AppPassword, ct);

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadWrite, ct);

                // Try to move to Trash if available, otherwise mark Deleted and Expunge
                IMailFolder? trashFolder = null;
                try
                {
                    trashFolder = client.GetFolder(SpecialFolder.Trash);
                }
                catch { }

                if (trashFolder != null && trashFolder.FullName != inbox.FullName)
                {
                    await inbox.MoveToAsync(uidList, trashFolder, ct);
                }
                else
                {
                    await inbox.AddFlagsAsync(uidList, MessageFlags.Deleted, true, ct);
                    await inbox.ExpungeAsync(ct);
                }

                await client.DisconnectAsync(true, ct);
                logger?.Report($"[✓] {account.Name}: Successfully deleted {uidList.Count} message(s).");
                return true;
            }
            catch (OperationCanceledException)
            {
                logger?.Report($"[!] {account.Name}: Deletion cancelled.");
                return false;
            }
            catch (Exception ex)
            {
                logger?.Report($"[!] {account.Name} Delete Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ArchiveEmailsAsync(
            EmailAccount account,
            IEnumerable<uint> uids,
            IProgress<string>? logger = null,
            CancellationToken ct = default)
        {
            var uidList = uids.Select(u => new UniqueId(u)).ToList();
            if (uidList.Count == 0 || !account.IsEnabled) return true;

            using var client = new ImapClient();
            try
            {
                logger?.Report($"[*] Connecting to {account.Name} to archive {uidList.Count} message(s)...");
                client.Timeout = 15000;
                var sslOption = account.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

                await client.ConnectAsync(account.Host, account.Port, sslOption, ct);
                await client.AuthenticateAsync(account.Email, account.AppPassword, ct);

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadWrite, ct);

                // Look for standard Archive or All Mail folder
                IMailFolder? targetFolder = null;
                try
                {
                    targetFolder = client.GetFolder(SpecialFolder.Archive);
                }
                catch { }

                if (targetFolder == null)
                {
                    try
                    {
                        targetFolder = client.GetFolder(SpecialFolder.All);
                    }
                    catch { }
                }

                if (targetFolder == null)
                {
                    try
                    {
                        var personal = client.GetFolder(client.PersonalNamespaces.FirstOrDefault()?.Path ?? "");
                        var subfolders = await personal.GetSubfoldersAsync(false, ct);
                        targetFolder = subfolders.FirstOrDefault(f =>
                            f.Name.Equals("Archive", StringComparison.OrdinalIgnoreCase) ||
                            f.Name.Equals("Archives", StringComparison.OrdinalIgnoreCase) ||
                            f.Name.Equals("All Mail", StringComparison.OrdinalIgnoreCase));
                    }
                    catch { }
                }

                if (targetFolder != null && targetFolder.FullName != inbox.FullName)
                {
                    await inbox.MoveToAsync(uidList, targetFolder, ct);
                }
                else
                {
                    // If no dedicated archive folder exists, mark as Seen and remove from inbox via Deleted+Expunge
                    await inbox.AddFlagsAsync(uidList, MessageFlags.Seen, true, ct);
                    await inbox.AddFlagsAsync(uidList, MessageFlags.Deleted, true, ct);
                    await inbox.ExpungeAsync(ct);
                }

                await client.DisconnectAsync(true, ct);
                logger?.Report($"[✓] {account.Name}: Successfully archived {uidList.Count} message(s).");
                return true;
            }
            catch (OperationCanceledException)
            {
                logger?.Report($"[!] {account.Name}: Archive cancelled.");
                return false;
            }
            catch (Exception ex)
            {
                logger?.Report($"[!] {account.Name} Archive Error: {ex.Message}");
                return false;
            }
        }

        public async Task DeleteEmailsBatchAsync(
            IEnumerable<EmailItem> emails,
            IEnumerable<EmailAccount> accounts,
            IProgress<string>? logger = null,
            CancellationToken ct = default)
        {
            var emailList = emails.ToList();
            if (emailList.Count == 0) return;

            var accList = accounts.ToList();
            var grouped = emailList.GroupBy(e => 
                accList.FirstOrDefault(a => 
                    string.Equals(a.Email, e.AccountEmail, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.Name, e.AccountName, StringComparison.OrdinalIgnoreCase)));

            var tasks = new List<Task>();
            foreach (var group in grouped)
            {
                var account = group.Key;
                if (account == null) continue;

                var uids = group.Select(e => e.UniqueId).Distinct().ToList();
                tasks.Add(DeleteEmailsAsync(account, uids, logger, ct));
            }

            await Task.WhenAll(tasks);
        }

        public async Task ArchiveEmailsBatchAsync(
            IEnumerable<EmailItem> emails,
            IEnumerable<EmailAccount> accounts,
            IProgress<string>? logger = null,
            CancellationToken ct = default)
        {
            var emailList = emails.ToList();
            if (emailList.Count == 0) return;

            var accList = accounts.ToList();
            var grouped = emailList.GroupBy(e => 
                accList.FirstOrDefault(a => 
                    string.Equals(a.Email, e.AccountEmail, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.Name, e.AccountName, StringComparison.OrdinalIgnoreCase)));

            var tasks = new List<Task>();
            foreach (var group in grouped)
            {
                var account = group.Key;
                if (account == null) continue;

                var uids = group.Select(e => e.UniqueId).Distinct().ToList();
                tasks.Add(ArchiveEmailsAsync(account, uids, logger, ct));
            }

            await Task.WhenAll(tasks);
        }

        private static string ExtractAndCleanBody(MimeMessage message)
        {
            string raw = message.TextBody ?? string.Empty;

            if (string.IsNullOrWhiteSpace(raw) && !string.IsNullOrWhiteSpace(message.HtmlBody))
            {
                raw = ConvertHtmlToPlainText(message.HtmlBody);
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return "(No text content in this email)";
            }

            // Strip tracking URLs and normalize whitespace
            raw = Regex.Replace(raw, @"https?://[^\s]+", "[link]");
            raw = Regex.Replace(raw, @"\r\n|\r|\n", "\r\n");
            raw = Regex.Replace(raw, @"(\r\n){3,}", "\r\n\r\n");

            // Cap at 4,000 chars for efficient model context ingestion
            if (raw.Length > 4000)
            {
                raw = raw.Substring(0, 4000) + "\r\n... [email truncated for length]";
            }

            return raw.Trim();
        }

        private static string ConvertHtmlToPlainText(string html)
        {
            html = Regex.Replace(html, @"<style[^>]*>.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            html = Regex.Replace(html, @"<br\s*/?>", "\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</p>", "\r\n\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</div>", "\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</tr>", "\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</li>", "\r\n", RegexOptions.IgnoreCase);

            html = Regex.Replace(html, @"<[^>]+>", " ");
            html = System.Net.WebUtility.HtmlDecode(html);
            return html;
        }
    }
}
