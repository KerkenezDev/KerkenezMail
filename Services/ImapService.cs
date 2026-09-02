using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            CancellationToken ct = default,
            MailFolderType folderType = MailFolderType.Inbox)
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

                var targetFolder = await ResolveFolderAsync(client, folderType, ct);
                if (targetFolder == null)
                {
                    logger?.Report($"[-] {account.Name}: {folderType.GetDisplayName()} folder not found on server.");
                    await client.DisconnectAsync(true, ct);
                    return emails;
                }

                await targetFolder.OpenAsync(FolderAccess.ReadWrite, ct);

                // Fetch either unread or all messages (for non-Inbox folders, always fetch all messages)
                var searchQuery = (folderType == MailFolderType.Inbox && settings.OnlyUnread)
                    ? SearchQuery.NotSeen
                    : SearchQuery.All;

                var uids = await targetFolder.SearchAsync(searchQuery, ct);

                if (uids.Count == 0)
                {
                    logger?.Report($"[✓] {account.Name}: {folderType.GetDisplayName()} is empty.");
                    await client.DisconnectAsync(true, ct);
                    return emails;
                }

                // Take latest N messages (highest UIDs)
                var targetUids = uids.Reverse().Take(settings.MaxEmailsPerAccount).ToList();
                logger?.Report($"[*] {account.Name} ({folderType.GetDisplayName()}): Found {uids.Count} total messages. Fetching {targetUids.Count} recent emails...");

                // Batch fetch flags (Seen/Unseen) for all target UIDs
                var summaries = await targetFolder.FetchAsync(targetUids, MessageSummaryItems.Flags | MessageSummaryItems.UniqueId, ct);
                var flagDict = summaries.ToDictionary(s => s.UniqueId, s => s.Flags);

                // Stream full MIME messages progressively
                foreach (var uid in targetUids)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var message = await targetFolder.GetMessageAsync(uid, ct);

                        bool isRead = false;
                        if (flagDict.TryGetValue(uid, out var flags) && flags.HasValue)
                        {
                            isRead = flags.Value.HasFlag(MessageFlags.Seen);
                        }

                        string cleanBody = ExtractAndCleanBody(message);
                        var (displayText, displayRtf, extractedLinks) = ExtractDisplayContent(message);
                        bool isMailingList = DetectMailingListHeaders(message);
                        bool hasNewsletterFooter = DetectNewsletterFooter(cleanBody) || DetectNewsletterFooter(message.HtmlBody);

                        // Extract attachment metadata
                        var detectedAttachments = new List<EmailAttachmentInfo>();
                        int partIdx = 0;
                        foreach (var attachment in message.Attachments)
                        {
                            string fileName = "";
                            long size = 0;
                            string mimeType = attachment.ContentType?.MimeType ?? "application/octet-stream";
                            string? contentId = attachment.ContentId;

                            if (attachment is MimePart mimePart)
                            {
                                fileName = mimePart.FileName ?? mimePart.ContentDisposition?.FileName ?? "";
                                if (mimePart.Content != null && mimePart.Content.Stream != null)
                                {
                                    try { size = mimePart.Content.Stream.Length; } catch { }
                                }
                                if (size == 0 && mimePart.ContentDisposition?.Size != null)
                                {
                                    size = mimePart.ContentDisposition.Size.Value;
                                }
                            }
                            else if (attachment is MessagePart msgPart)
                            {
                                fileName = msgPart.ContentDisposition?.FileName ?? "message.eml";
                            }

                            if (string.IsNullOrWhiteSpace(fileName))
                            {
                                fileName = $"attachment_{partIdx + 1}";
                            }

                            detectedAttachments.Add(new EmailAttachmentInfo
                            {
                                FileName = fileName,
                                FileSizeBytes = size,
                                MimeType = mimeType,
                                ContentId = contentId,
                                PartIndex = partIdx++
                            });
                        }

                        var emailItem = new EmailItem
                        {
                            UniqueId = uid.Id,
                            AccountName = account.Name,
                            AccountEmail = account.Email,
                            Subject = string.IsNullOrWhiteSpace(message.Subject) ? "(No Subject)" : message.Subject,
                            Sender = message.From.Mailboxes.FirstOrDefault()?.ToString() ?? "(Unknown Sender)",
                            Date = message.Date,
                            MessageId = message.MessageId,
                            InReplyTo = message.InReplyTo,
                            References = message.References?.ToList() ?? new List<string>(),
                            RawBody = message.TextBody ?? message.HtmlBody ?? string.Empty,
                            HtmlBody = message.HtmlBody,
                            CleanBody = cleanBody,
                            DisplayBody = displayText,
                            DisplayRtf = displayRtf,
                            ExtractedLinks = extractedLinks,
                            Attachments = detectedAttachments,
                            IsRead = isRead,
                            Folder = folderType,
                            Status = SummaryState.Pending,
                            IsMailingList = isMailingList,
                            HasNewsletterFooter = hasNewsletterFooter
                        };

                        emails.Add(emailItem);

                        // Stream immediately to UI in real-time!
                        onEmailFetched?.Invoke(emailItem);

                        if (settings.MarkAsSeen && !isRead && folderType == MailFolderType.Inbox)
                        {
                            await targetFolder.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
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
            CancellationToken ct = default,
            MailFolderType folderType = MailFolderType.Inbox)
        {
            var tasks = accounts
                .Where(a => a.IsEnabled)
                .Select(acc => FetchEmailsFromAccountAsync(acc, settings, logger, onEmailFetched, ct, folderType));

            var results = await Task.WhenAll(tasks);
            var allEmails = results.SelectMany(r => r)
                .OrderByDescending(e => e.Date)
                .ToList();

            return allEmails;
        }

        public async Task<bool> DeleteEmailsAsync(
            EmailAccount account,
            IEnumerable<uint> uids,
            MailFolderType folderType = MailFolderType.Inbox,
            IProgress<string>? logger = null,
            CancellationToken ct = default)
        {
            var uidList = uids.Where(u => u > 0).Select(u => new UniqueId(u)).ToList();
            if (uidList.Count == 0 || !account.IsEnabled) return true;

            using var client = new ImapClient();
            try
            {
                logger?.Report($"[*] Connecting to {account.Name} to delete {uidList.Count} message(s) from {folderType.GetDisplayName()}...");
                client.Timeout = 15000;
                var sslOption = account.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

                await client.ConnectAsync(account.Host, account.Port, sslOption, ct);
                await client.AuthenticateAsync(account.Email, account.AppPassword, ct);

                var sourceFolder = await ResolveFolderAsync(client, folderType, ct);
                if (sourceFolder == null)
                {
                    logger?.Report($"[!] {account.Name}: Could not resolve folder '{folderType.GetDisplayName()}' for deletion.");
                    return false;
                }

                await sourceFolder.OpenAsync(FolderAccess.ReadWrite, ct);

                if (folderType == MailFolderType.Trash)
                {
                    // Already in Trash: permanently delete
                    await sourceFolder.AddFlagsAsync(uidList, MessageFlags.Deleted, true, ct);
                    await sourceFolder.ExpungeAsync(ct);
                }
                else
                {
                    // Try to move to Trash if available, otherwise mark Deleted and Expunge
                    IMailFolder? trashFolder = null;
                    try
                    {
                        trashFolder = await ResolveFolderAsync(client, MailFolderType.Trash, ct);
                    }
                    catch { }

                    bool moved = false;
                    if (trashFolder != null && trashFolder.FullName != sourceFolder.FullName)
                    {
                        try
                        {
                            await sourceFolder.MoveToAsync(uidList, trashFolder, ct);
                            moved = true;
                        }
                        catch (Exception ex)
                        {
                            logger?.Report($"[!] {account.Name}: Move to Trash failed ({ex.Message}), falling back to direct delete.");
                        }
                    }

                    if (!moved)
                    {
                        await sourceFolder.AddFlagsAsync(uidList, MessageFlags.Deleted, true, ct);
                        await sourceFolder.ExpungeAsync(ct);
                    }
                }

                await client.DisconnectAsync(true, ct);
                logger?.Report($"[✓] {account.Name}: Successfully deleted {uidList.Count} message(s) from {folderType.GetDisplayName()}.");
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
            MailFolderType folderType = MailFolderType.Inbox,
            IProgress<string>? logger = null,
            CancellationToken ct = default)
        {
            var uidList = uids.Where(u => u > 0).Select(u => new UniqueId(u)).ToList();
            if (uidList.Count == 0 || !account.IsEnabled) return true;

            using var client = new ImapClient();
            try
            {
                logger?.Report($"[*] Connecting to {account.Name} to archive {uidList.Count} message(s) from {folderType.GetDisplayName()}...");
                client.Timeout = 15000;
                var sslOption = account.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

                await client.ConnectAsync(account.Host, account.Port, sslOption, ct);
                await client.AuthenticateAsync(account.Email, account.AppPassword, ct);

                var sourceFolder = await ResolveFolderAsync(client, folderType, ct);
                if (sourceFolder == null)
                {
                    logger?.Report($"[!] {account.Name}: Could not resolve folder '{folderType.GetDisplayName()}' for archival.");
                    return false;
                }

                await sourceFolder.OpenAsync(FolderAccess.ReadWrite, ct);

                // Look for standard Archive folder
                IMailFolder? targetFolder = null;
                try
                {
                    targetFolder = await ResolveFolderAsync(client, MailFolderType.Archive, ct);
                }
                catch { }

                bool moved = false;
                if (targetFolder != null && targetFolder.FullName != sourceFolder.FullName)
                {
                    try
                    {
                        await sourceFolder.MoveToAsync(uidList, targetFolder, ct);
                        moved = true;
                    }
                    catch (Exception ex)
                    {
                        logger?.Report($"[!] {account.Name}: Move to Archive failed ({ex.Message}), falling back to mark seen.");
                    }
                }

                if (!moved)
                {
                    // If no dedicated archive folder exists or move failed, mark as Seen and remove from folder via Deleted+Expunge
                    await sourceFolder.AddFlagsAsync(uidList, MessageFlags.Seen, true, ct);
                    await sourceFolder.AddFlagsAsync(uidList, MessageFlags.Deleted, true, ct);
                    await sourceFolder.ExpungeAsync(ct);
                }

                await client.DisconnectAsync(true, ct);
                logger?.Report($"[✓] {account.Name}: Successfully archived {uidList.Count} message(s) from {folderType.GetDisplayName()}.");
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
            var grouped = emailList.GroupBy(e => (
                Account: accList.FirstOrDefault(a => 
                    (!string.IsNullOrEmpty(e.AccountEmail) && string.Equals(a.Email, e.AccountEmail, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(e.AccountName) && string.Equals(a.Name, e.AccountName, StringComparison.OrdinalIgnoreCase))),
                Folder: e.Folder
            ));

            var tasks = new List<Task>();
            foreach (var group in grouped)
            {
                var account = group.Key.Account;
                var folder = group.Key.Folder;
                if (account == null) continue;

                var uids = group.Select(e => e.UniqueId).Where(u => u > 0).Distinct().ToList();
                if (uids.Count == 0) continue;

                tasks.Add(DeleteEmailsAsync(account, uids, folder, logger, ct));
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
            var grouped = emailList.GroupBy(e => (
                Account: accList.FirstOrDefault(a => 
                    (!string.IsNullOrEmpty(e.AccountEmail) && string.Equals(a.Email, e.AccountEmail, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(e.AccountName) && string.Equals(a.Name, e.AccountName, StringComparison.OrdinalIgnoreCase))),
                Folder: e.Folder
            ));

            var tasks = new List<Task>();
            foreach (var group in grouped)
            {
                var account = group.Key.Account;
                var folder = group.Key.Folder;
                if (account == null) continue;

                var uids = group.Select(e => e.UniqueId).Where(u => u > 0).Distinct().ToList();
                if (uids.Count == 0) continue;

                tasks.Add(ArchiveEmailsAsync(account, uids, folder, logger, ct));
            }

            await Task.WhenAll(tasks);
        }

        private static string ExtractAndCleanBody(MimeMessage message)
        {
            string raw = message.TextBody ?? string.Empty;

            if (string.IsNullOrWhiteSpace(raw) && !string.IsNullOrWhiteSpace(message.HtmlBody))
            {
                raw = ConvertHtmlToDisplayText(message.HtmlBody);
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return "(No text content in this email)";
            }

            // Strip tracking URLs and normalize whitespace
            raw = Regex.Replace(raw, @"https?://[^\s]+", "[link]");
            raw = Regex.Replace(raw, @"\r\n|\r|\n", "\r\n");
            raw = Regex.Replace(raw, @"(\r\n){3,}", "\r\n\r\n");

            return raw.Trim();
        }

        public static (string DisplayText, string? DisplayRtf, List<EmailLink> Links) ExtractDisplayContent(MimeMessage message)
        {
            string html = message.HtmlBody ?? string.Empty;
            string text = message.TextBody ?? string.Empty;
            var links = new List<EmailLink>();

            if (!string.IsNullOrWhiteSpace(html))
            {
                try
                {
                    string rtf = ConvertHtmlToRtf(html, links);
                    string displayText = ConvertHtmlToDisplayText(html);
                    return (displayText, rtf, links);
                }
                catch
                {
                    // Fallback to text if RTF generation has any issue
                }
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    string rtf = ConvertPlainTextToRtf(text, links);
                    return (text.Trim(), rtf, links);
                }
                catch
                {
                    return (text.Trim(), null, links);
                }
            }

            return ("(No text content in this email)", null, links);
        }

        private static string ConvertHtmlToRtf(string html, List<EmailLink>? links = null)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            // Remove scripts, styles, head, xml metadata
            html = Regex.Replace(html, @"<style[^>]*>.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<head[^>]*>.*?</head>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<xml[^>]*>.*?</xml>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Replace anchor tags with RTF hyperlink field markers
            html = Regex.Replace(html, @"<a\s+[^>]*href=(?:""([^""]*)""|'([^']*)'|([^\s>]+))[^>]*>(.*?)</a>", m =>
            {
                string href = m.Groups[1].Value;
                if (string.IsNullOrEmpty(href)) href = m.Groups[2].Value;
                if (string.IsNullOrEmpty(href)) href = m.Groups[3].Value;
                string innerHtml = m.Groups[4].Value;

                bool hasMedia = Regex.IsMatch(innerHtml, @"<(?:img|svg|picture|figure|video)\b", RegexOptions.IgnoreCase);
                if (hasMedia && IsTrackingPixel(innerHtml))
                {
                    return "";
                }

                string innerText = Regex.Replace(innerHtml, @"<[^>]+>", " ");
                innerText = System.Net.WebUtility.HtmlDecode(innerText).Trim();
                href = System.Net.WebUtility.HtmlDecode(href).Trim();

                if (string.IsNullOrWhiteSpace(href)) return innerText;

                string altText = hasMedia ? GetCleanAltText(innerHtml) : "";
                string label;

                if (hasMedia && string.IsNullOrWhiteSpace(innerText))
                {
                    label = !string.IsNullOrWhiteSpace(altText) ? $"[Remote Content: {altText}]" : "[Remote Content]";
                }
                else if (!string.IsNullOrWhiteSpace(innerText))
                {
                    label = innerText;
                }
                else
                {
                    label = "[Remote Content]";
                }

                if (links != null && !links.Any(l => l.Text == label && l.Url == href))
                {
                    links.Add(new EmailLink { Text = label, Url = href });
                }

                string rtfHref = EscapeRtfUrl(href);
                string rtfText = EscapeRtf(label);

                return $@"@@RTFLINK_START@@{{\field{{\*\fldinst{{HYPERLINK ""{rtfHref}""}}}}{{\fldrslt{{\cf1\ul {rtfText}}}}}}}@@RTFLINK_END@@";
            }, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Replace standalone <img> tags with [Remote Content] hyperlink pointing to src
            html = Regex.Replace(html, @"<img\s+[^>]*src=(?:""([^""]*)""|'([^']*)'|([^\s>]+))[^>]*>", m =>
            {
                string fullTag = m.Value;
                if (IsTrackingPixel(fullTag))
                {
                    return "";
                }

                string src = m.Groups[1].Value;
                if (string.IsNullOrEmpty(src)) src = m.Groups[2].Value;
                if (string.IsNullOrEmpty(src)) src = m.Groups[3].Value;
                src = System.Net.WebUtility.HtmlDecode(src).Trim();

                if (string.IsNullOrWhiteSpace(src) || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    return "";
                }

                string altText = GetCleanAltText(fullTag);
                string label = !string.IsNullOrWhiteSpace(altText) ? $"[Remote Content: {altText}]" : "[Remote Content]";

                if (links != null && !links.Any(l => l.Text == label && l.Url == src))
                {
                    links.Add(new EmailLink { Text = label, Url = src });
                }

                string rtfHref = EscapeRtfUrl(src);
                string rtfText = EscapeRtf(label);

                return $@"@@RTFLINK_START@@{{\field{{\*\fldinst{{HYPERLINK ""{rtfHref}""}}}}{{\fldrslt{{\cf1\ul {rtfText}}}}}}}@@RTFLINK_END@@";
            }, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Structural tags
            html = Regex.Replace(html, @"<h[1-3][^>]*>", "\r\n\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</h[1-3]>", "\r\n\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<h[4-6][^>]*>", "\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</h[4-6]>", "\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<br\s*/?>", "\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</p>", "\r\n\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</div>", "\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</tr>", "\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<li[^>]*>", "\r\n• ", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</li>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<hr\s*/?>", "\r\n---\r\n", RegexOptions.IgnoreCase);

            // Strip remaining HTML tags
            html = Regex.Replace(html, @"<[^>]+>", " ");

            // Process text segments while preserving @@RTFLINK markers
            var chunks = Regex.Split(html, @"(@@RTFLINK_START@@.*?@@RTFLINK_END@@)", RegexOptions.Singleline);
            var bodySb = new StringBuilder();

            foreach (var chunk in chunks)
            {
                if (chunk.StartsWith("@@RTFLINK_START@@") && chunk.EndsWith("@@RTFLINK_END@@"))
                {
                    string linkField = chunk.Substring("@@RTFLINK_START@@".Length, chunk.Length - "@@RTFLINK_START@@".Length - "@@RTFLINK_END@@".Length);
                    bodySb.Append(linkField);
                }
                else
                {
                    string decoded = System.Net.WebUtility.HtmlDecode(chunk);
                    bodySb.Append(EscapeRtf(decoded));
                }
            }

            string rtfBody = bodySb.ToString();
            // Normalize multiple paragraphs
            rtfBody = Regex.Replace(rtfBody, @"(\\par\s*){3,}", @"\par\par " + Environment.NewLine);

            var fullRtf = new StringBuilder();
            fullRtf.AppendLine(@"{\rtf1\ansi\ansicpg1252\deff0\nouicompat\deflang1033{\fonttbl{\f0\fnil\fcharset0 Segoe UI;}}");
            fullRtf.AppendLine(@"{\colortbl ;\red0\green102\blue204;\red30\green30\blue30;}");
            fullRtf.AppendLine(@"\viewkind4\uc1 ");
            fullRtf.AppendLine(@"\pard\cf2\f0\fs20 ");
            fullRtf.Append(rtfBody.Trim());
            fullRtf.AppendLine(@"\par");
            fullRtf.AppendLine(@"}");

            return fullRtf.ToString();
        }

        private static string ConvertPlainTextToRtf(string text, List<EmailLink>? links = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var chunks = Regex.Split(text, @"(https?://[^\s<>""'{}|\^\[\]`]+)");
            var bodySb = new StringBuilder();

            foreach (var chunk in chunks)
            {
                if (Regex.IsMatch(chunk, @"^https?://", RegexOptions.IgnoreCase))
                {
                    if (links != null && !links.Any(l => l.Url == chunk))
                    {
                        links.Add(new EmailLink { Text = chunk, Url = chunk });
                    }
                    string rtfHref = EscapeRtfUrl(chunk);
                    string rtfText = EscapeRtf(chunk);
                    bodySb.Append($@"{{\field{{\*\fldinst{{HYPERLINK ""{rtfHref}""}}}}{{\fldrslt{{\cf1\ul {rtfText}}}}}}}");
                }
                else
                {
                    bodySb.Append(EscapeRtf(chunk));
                }
            }

            var fullRtf = new StringBuilder();
            fullRtf.AppendLine(@"{\rtf1\ansi\ansicpg1252\deff0\nouicompat\deflang1033{\fonttbl{\f0\fnil\fcharset0 Segoe UI;}}");
            fullRtf.AppendLine(@"{\colortbl ;\red0\green102\blue204;\red30\green30\blue30;}");
            fullRtf.AppendLine(@"\viewkind4\uc1 ");
            fullRtf.AppendLine(@"\pard\cf2\f0\fs20 ");
            fullRtf.Append(bodySb.ToString().Trim());
            fullRtf.AppendLine(@"\par");
            fullRtf.AppendLine(@"}");

            return fullRtf.ToString();
        }

        private static string ConvertHtmlToDisplayText(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;

            html = Regex.Replace(html, @"<style[^>]*>.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<head[^>]*>.*?</head>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // 1. Turn anchor tags into Text or [Remote Content]
            html = Regex.Replace(html, @"<a\s+[^>]*href=(?:""([^""]*)""|'([^']*)'|([^\s>]+))[^>]*>(.*?)</a>", m =>
            {
                string href = m.Groups[1].Value;
                if (string.IsNullOrEmpty(href)) href = m.Groups[2].Value;
                if (string.IsNullOrEmpty(href)) href = m.Groups[3].Value;
                string innerHtml = m.Groups[4].Value;

                bool hasMedia = Regex.IsMatch(innerHtml, @"<(?:img|svg|picture|figure|video)\b", RegexOptions.IgnoreCase);
                if (hasMedia && IsTrackingPixel(innerHtml)) return "";

                string innerText = Regex.Replace(innerHtml, @"<[^>]+>", " ");
                innerText = System.Net.WebUtility.HtmlDecode(innerText).Trim();
                href = System.Net.WebUtility.HtmlDecode(href).Trim();

                if (hasMedia && string.IsNullOrWhiteSpace(innerText))
                {
                    string alt = GetCleanAltText(innerHtml);
                    return !string.IsNullOrWhiteSpace(alt) ? $"[Remote Content: {alt}]" : "[Remote Content]";
                }

                if (string.IsNullOrWhiteSpace(innerText) || string.Equals(innerText, href, StringComparison.OrdinalIgnoreCase))
                {
                    return "[Remote Content]";
                }
                return innerText;
            }, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // 2. Turn standalone <img> into [Remote Content]
            html = Regex.Replace(html, @"<img\s+[^>]*src=(?:""([^""]*)""|'([^']*)'|([^\s>]+))[^>]*>", m =>
            {
                if (IsTrackingPixel(m.Value)) return "";

                string alt = GetCleanAltText(m.Value);
                return !string.IsNullOrWhiteSpace(alt) ? $"[Remote Content: {alt}]" : "[Remote Content]";
            }, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            html = Regex.Replace(html, @"<br\s*/?>", "\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</p>", "\r\n\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</div>", "\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</tr>", "\r\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<li[^>]*>", "\r\n• ", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</li>", "", RegexOptions.IgnoreCase);

            html = Regex.Replace(html, @"<[^>]+>", " ");
            html = System.Net.WebUtility.HtmlDecode(html);
            html = Regex.Replace(html, @"\r\n|\r|\n", "\r\n");
            html = Regex.Replace(html, @"(\r\n){3,}", "\r\n\r\n");
            return html.Trim();
        }

        private static string EscapeRtf(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length * 2);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '\\': sb.Append(@"\\"); break;
                    case '{': sb.Append(@"\{"); break;
                    case '}': sb.Append(@"\}"); break;
                    case '\r': break;
                    case '\n': sb.Append(@"\par" + Environment.NewLine); break;
                    case '\t': sb.Append(@"\tab "); break;
                    default:
                        if (c > 127)
                        {
                            sb.Append(@"\u").Append((short)c).Append('?');
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        private static string EscapeRtfUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return string.Empty;
            return url.Replace(@"\", @"\\")
                      .Replace(@"""", @"\""")
                      .Replace("{", @"\{")
                      .Replace("}", @"\}");
        }

        private static bool DetectMailingListHeaders(MimeMessage message)
        {
            if (message.Headers == null) return false;

            if (message.Headers.Contains(HeaderId.ListUnsubscribe) ||
                message.Headers.Contains(HeaderId.ListId) ||
                message.Headers.Contains(HeaderId.ListPost) ||
                message.Headers.Contains("List-Unsubscribe") ||
                message.Headers.Contains("List-ID") ||
                message.Headers.Contains("List-Post") ||
                message.Headers.Contains("Feedback-ID") ||
                message.Headers.Contains("X-Campaign") ||
                message.Headers.Contains("X-Mailgun-Tag") ||
                message.Headers.Contains("X-SES-Outgoing"))
            {
                return true;
            }

            if (message.Headers.Contains(HeaderId.Precedence))
            {
                string prec = message.Headers[HeaderId.Precedence] ?? "";
                if (prec.Contains("bulk", StringComparison.OrdinalIgnoreCase) ||
                    prec.Contains("list", StringComparison.OrdinalIgnoreCase) ||
                    prec.Contains("junk", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool DetectNewsletterFooter(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            return Regex.IsMatch(
                text,
                @"(?:unsubscribe|opt[\s\-]out|email\s+preferences|manage\s+(?:your\s+)?subscription|manage\s+preferences|view\s+(?:this\s+email\s+)?in\s+browser|view\s+online|all\s+rights\s+reserved|privacy\s+policy\s*[|•\/\-]\s*terms|to\s+stop\s+receiving\s+these\s+emails|you\s+are\s+receiving\s+this\s+email\s+because|click\s+here\s+to\s+unsubscribe)",
                RegexOptions.IgnoreCase | RegexOptions.Multiline
            );
        }

        private static bool IsTrackingPixel(string tagOrHtml)
        {
            if (string.IsNullOrWhiteSpace(tagOrHtml)) return false;

            // Dimensions: width="0", width="1", height="0", height="1"
            if (Regex.IsMatch(tagOrHtml, @"\b(?:width|height)\s*=\s*[""']?[01](?:px)?[""']?", RegexOptions.IgnoreCase))
            {
                return true;
            }

            // Inline styles: display:none, width:0px, width:1px, height:0px, height:1px
            if (Regex.IsMatch(tagOrHtml, @"style\s*=\s*[""'][^""']*(?:display\s*:\s*none|width\s*:\s*[01]px|height\s*:\s*[01]px)", RegexOptions.IgnoreCase))
            {
                return true;
            }

            // Common tracking keywords in img source/tag
            if (Regex.IsMatch(tagOrHtml, @"(?:/pixel\b|/trk\b|/open\b|spacer\.gif|blank\.gif|clear\.gif)", RegexOptions.IgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static string GetCleanAltText(string tagOrHtml)
        {
            if (string.IsNullOrWhiteSpace(tagOrHtml)) return string.Empty;

            var match = Regex.Match(tagOrHtml, @"alt=(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase);
            if (!match.Success) return string.Empty;

            string alt = match.Groups[1].Value;
            if (string.IsNullOrEmpty(alt)) alt = match.Groups[2].Value;
            if (string.IsNullOrEmpty(alt)) alt = match.Groups[3].Value;

            alt = System.Net.WebUtility.HtmlDecode(alt).Trim();

            // Ignore generic placeholders or excessively long alt texts
            if (string.IsNullOrWhiteSpace(alt) || 
                alt.Equals("image", StringComparison.OrdinalIgnoreCase) ||
                alt.Equals("spacer", StringComparison.OrdinalIgnoreCase) ||
                alt.Equals("blank", StringComparison.OrdinalIgnoreCase) ||
                alt.Equals("picture", StringComparison.OrdinalIgnoreCase) ||
                alt.Length > 40)
            {
                return string.Empty;
            }

            return alt;
        }

        /// <summary>
        /// Downloads an email attachment on demand using a single-shot IMAP connection.
        /// Immediately disconnects once streaming finishes. Zero persistent idle ports.
        /// </summary>
        public async Task<(bool Success, string Message)> DownloadAttachmentAsync(
            EmailAccount account,
            uint uniqueId,
            int partIndex,
            string fileName,
            string targetFilePath,
            IProgress<string>? logger = null,
            CancellationToken ct = default)
        {
            using var client = new ImapClient();
            try
            {
                client.Timeout = 30000;
                var sslOption = account.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

                logger?.Report($"[*] Connecting to {account.Name} to download attachment '{fileName}'...");
                await client.ConnectAsync(account.Host, account.Port, sslOption, ct);
                await client.AuthenticateAsync(account.Email, account.AppPassword, ct);

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

                var message = await inbox.GetMessageAsync(new UniqueId(uniqueId), ct);
                var attachments = message.Attachments.ToList();

                MimeEntity? targetEntity = null;
                if (partIndex >= 0 && partIndex < attachments.Count)
                {
                    targetEntity = attachments[partIndex];
                }
                else
                {
                    targetEntity = attachments.FirstOrDefault(a =>
                        (a is MimePart mp && string.Equals(mp.FileName, fileName, StringComparison.OrdinalIgnoreCase)) ||
                        (a.ContentDisposition != null && string.Equals(a.ContentDisposition.FileName, fileName, StringComparison.OrdinalIgnoreCase)));
                }

                if (targetEntity == null)
                {
                    await client.DisconnectAsync(true, ct);
                    return (false, $"Attachment '{fileName}' not found in message.");
                }

                using (var outputStream = File.Create(targetFilePath))
                {
                    if (targetEntity is MimePart mimePart && mimePart.Content != null)
                    {
                        await mimePart.Content.DecodeToAsync(outputStream, ct);
                    }
                    else if (targetEntity is MessagePart msgPart && msgPart.Message != null)
                    {
                        await msgPart.Message.WriteToAsync(outputStream, ct);
                    }
                    else
                    {
                        await targetEntity.WriteToAsync(outputStream, ct);
                    }
                }

                await client.DisconnectAsync(true, ct);
                logger?.Report($"[✓] Saved '{fileName}' to {targetFilePath}.");
                return (true, $"Attachment downloaded successfully: {fileName}");
            }
            catch (Exception ex)
            {
                logger?.Report($"[!] Failed to download attachment: {ex.Message}");
                return (false, $"Download error: {ex.Message}");
            }
        }

        /// <summary>
        /// Lightweight on-demand fetch of the original HTML body of an email for browser rendering.
        /// </summary>
        public async Task<string?> FetchEmailHtmlBodyAsync(
            EmailAccount account,
            uint uniqueId,
            MailFolderType folderType = MailFolderType.Inbox,
            CancellationToken ct = default)
        {
            using var client = new ImapClient();
            try
            {
                client.Timeout = 20000;
                var sslOption = account.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

                await client.ConnectAsync(account.Host, account.Port, sslOption, ct);
                await client.AuthenticateAsync(account.Email, account.AppPassword, ct);

                var folder = await ResolveFolderAsync(client, folderType, ct);
                if (folder == null) return null;

                await folder.OpenAsync(FolderAccess.ReadOnly, ct);

                var message = await folder.GetMessageAsync(new UniqueId(uniqueId), ct);
                await client.DisconnectAsync(true, ct);

                return message.HtmlBody;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<IMailFolder?> ResolveFolderAsync(ImapClient client, MailFolderType folderType, CancellationToken ct = default)
        {
            switch (folderType)
            {
                case MailFolderType.Inbox:
                    return client.Inbox;

                case MailFolderType.Sent:
                {
                    try
                    {
                        var f = client.GetFolder(SpecialFolder.Sent);
                        if (f != null) return f;
                    }
                    catch { }

                    return await FindFolderByNamesAsync(client, ct, "sent", "sent items", "sent messages", "gesendet", "inbox.sent");
                }

                case MailFolderType.Archive:
                {
                    try
                    {
                        var f = client.GetFolder(SpecialFolder.Archive);
                        if (f != null) return f;
                    }
                    catch { }

                    try
                    {
                        var f = client.GetFolder(SpecialFolder.All);
                        if (f != null) return f;
                    }
                    catch { }

                    return await FindFolderByNamesAsync(client, ct, "archive", "archives", "all mail", "inbox.archive", "archiv");
                }

                case MailFolderType.Spam:
                {
                    try
                    {
                        var f = client.GetFolder(SpecialFolder.Junk);
                        if (f != null) return f;
                    }
                    catch { }

                    return await FindFolderByNamesAsync(client, ct, "junk", "spam", "bulk", "junk email", "junk e-mail", "inbox.junk", "unerwünscht");
                }

                case MailFolderType.Trash:
                {
                    try
                    {
                        var f = client.GetFolder(SpecialFolder.Trash);
                        if (f != null) return f;
                    }
                    catch { }

                    return await FindFolderByNamesAsync(client, ct, "trash", "deleted", "deleted items", "bin", "inbox.trash", "papierkorb");
                }

                default:
                    return client.Inbox;
            }
        }

        private static async Task<IMailFolder?> FindFolderByNamesAsync(ImapClient client, CancellationToken ct, params string[] candidateNames)
        {
            try
            {
                foreach (var ns in client.PersonalNamespaces)
                {
                    var root = await client.GetFolderAsync(ns.Path, ct);
                    var subfolders = await root.GetSubfoldersAsync(false, ct);
                    foreach (var folder in subfolders)
                    {
                        foreach (var name in candidateNames)
                        {
                            if (string.Equals(folder.Name, name, StringComparison.OrdinalIgnoreCase))
                            {
                                return folder;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
