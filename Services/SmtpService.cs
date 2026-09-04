using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using KerkenezMail.Models;

namespace KerkenezMail.Services
{
    public class SmtpService
    {
        public async Task<(bool Success, string Message)> TestSmtpConnectionAsync(
            EmailAccount account,
            CancellationToken ct = default)
        {
            using var client = new SmtpClient();
            try
            {
                client.Timeout = 12000;
                string host = account.GetEffectiveSmtpHost();
                int port = account.GetEffectiveSmtpPort();
                var sslOption = port == 465 || account.SmtpUseSsl 
                    ? SecureSocketOptions.SslOnConnect 
                    : SecureSocketOptions.StartTlsWhenAvailable;

                await client.ConnectAsync(host, port, sslOption, ct);
                await OutlookOAuthService.AuthenticateMailServiceAsync(client, account, ct: ct);
                await client.DisconnectAsync(true, ct);

                return (true, $"SMTP Connected & Authenticated successfully ({host}:{port}).");
            }
            catch (Exception ex)
            {
                return (false, $"SMTP Connection Failed: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> SendEmailAsync(
            SendMailDraft draft,
            bool saveSentToImap = true,
            IProgress<string>? logger = null,
            CancellationToken ct = default)
        {
            if (draft.FromAccount == null)
            {
                return (false, "No sending account selected.");
            }

            if (string.IsNullOrWhiteSpace(draft.To))
            {
                return (false, "Please specify at least one recipient in the 'To' field.");
            }

            var account = draft.FromAccount;
            var message = new MimeMessage();

            try
            {
                // 1. Sender
                message.From.Add(new MailboxAddress(account.Name, account.Email));

                // 2. Recipients
                AddRecipients(message.To, draft.To);
                if (!string.IsNullOrWhiteSpace(draft.Cc)) AddRecipients(message.Cc, draft.Cc);
                if (!string.IsNullOrWhiteSpace(draft.Bcc)) AddRecipients(message.Bcc, draft.Bcc);

                // 3. Subject & Date
                message.Subject = draft.Subject ?? string.Empty;
                message.Date = DateTimeOffset.Now;

                // 4. RFC 5322 Threading Headers (In-Reply-To & References)
                if (!string.IsNullOrWhiteSpace(draft.InReplyTo))
                {
                    string cleanInReplyTo = draft.InReplyTo.Trim();
                    if (!cleanInReplyTo.StartsWith("<")) cleanInReplyTo = "<" + cleanInReplyTo;
                    if (!cleanInReplyTo.EndsWith(">")) cleanInReplyTo = cleanInReplyTo + ">";
                    message.InReplyTo = cleanInReplyTo;
                }

                if (draft.References != null && draft.References.Count > 0)
                {
                    foreach (var refId in draft.References)
                    {
                        if (string.IsNullOrWhiteSpace(refId)) continue;
                        string cleanRef = refId.Trim();
                        if (!cleanRef.StartsWith("<")) cleanRef = "<" + cleanRef;
                        if (!cleanRef.EndsWith(">")) cleanRef = cleanRef + ">";
                        message.References.Add(cleanRef);
                    }
                }

                // 5. Body & Markdown Conversion
                var bodyBuilder = new BodyBuilder();
                string plainText = MarkdownEmailConverter.ConvertToPlainText(draft.BodyMarkdown);
                bodyBuilder.TextBody = plainText;

                if (!draft.SendAsPlaintextOnly)
                {
                    string html = MarkdownEmailConverter.ConvertToHtml(draft.BodyMarkdown);
                    bodyBuilder.HtmlBody = html;
                }

                // 6. Attachments (Drag & Drop Base64 Encoder)
                if (draft.Attachments != null && draft.Attachments.Count > 0)
                {
                    foreach (var att in draft.Attachments)
                    {
                        if (File.Exists(att.FilePath))
                        {
                            logger?.Report($"[*] Encoding attachment: {att.FileName} ({att.FormattedSize})...");
                            bodyBuilder.Attachments.Add(att.FilePath);
                        }
                    }
                }

                message.Body = bodyBuilder.ToMessageBody();

                // 7. Single-Shot SMTP Send (No persistent sockets or idle ports)
                using (var smtp = new SmtpClient())
                {
                    smtp.Timeout = 25000;
                    string host = account.GetEffectiveSmtpHost();
                    int port = account.GetEffectiveSmtpPort();
                    var sslOption = port == 465 || account.SmtpUseSsl 
                        ? SecureSocketOptions.SslOnConnect 
                        : SecureSocketOptions.StartTlsWhenAvailable;

                    logger?.Report($"[*] Connecting to SMTP {host}:{port}...");
                    await smtp.ConnectAsync(host, port, sslOption, ct);

                    logger?.Report($"[*] Authenticating as {account.Email}...");
                    await OutlookOAuthService.AuthenticateMailServiceAsync(smtp, account, logger: logger, ct: ct);

                    logger?.Report($"[*] Sending message '{message.Subject}'...");
                    await smtp.SendAsync(message, ct);

                    await smtp.DisconnectAsync(true, ct);
                    logger?.Report($"[✓] Email sent successfully via SMTP.");
                }

                // 8. Optional: Single-Shot IMAP Sent Folder Append
                // Gmail and Microsoft Outlook automatically store messages sent through SMTP into Sent Mail.
                // For other providers (Yahoo, iCloud, Custom IMAP), append to Sent folder via single connection.
                if (saveSentToImap && 
                    !account.Host.Contains("gmail.com", StringComparison.OrdinalIgnoreCase) && 
                    !account.IsOutlookOAuth && 
                    !account.Host.Contains("office365.com", StringComparison.OrdinalIgnoreCase) &&
                    !account.Host.Contains("outlook.com", StringComparison.OrdinalIgnoreCase))
                {
                    await TryAppendToSentFolderAsync(account, message, logger, ct);
                }

                return (true, "Email sent successfully!");
            }
            catch (Exception ex)
            {
                logger?.Report($"[!] Failed to send email: {ex.Message}");
                return (false, $"Error sending email: {ex.Message}");
            }
        }

        private static void AddRecipients(InternetAddressList targetList, string addresses)
        {
            var parts = addresses.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                string trimmed = part.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                if (MailboxAddress.TryParse(trimmed, out var mailbox))
                {
                    targetList.Add(mailbox);
                }
                else
                {
                    // Fallback to simple address
                    targetList.Add(new MailboxAddress(trimmed, trimmed));
                }
            }
        }

        private static async Task TryAppendToSentFolderAsync(
            EmailAccount account,
            MimeMessage message,
            IProgress<string>? logger,
            CancellationToken ct)
        {
            try
            {
                using var imap = new ImapClient();
                imap.Timeout = 15000;
                var sslOption = account.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

                await imap.ConnectAsync(account.Host, account.Port, sslOption, ct);
                await OutlookOAuthService.AuthenticateMailServiceAsync(imap, account, logger: logger, ct: ct);

                var sentFolder = await ImapService.ResolveFolderAsync(imap, MailFolderType.Sent, ct);
                if (sentFolder != null)
                {
                    await sentFolder.OpenAsync(FolderAccess.ReadWrite, ct);
                    await sentFolder.AppendAsync(message, MessageFlags.Seen, ct);
                    logger?.Report("[✓] Appended copy to IMAP Sent folder.");
                }

                await imap.DisconnectAsync(true, ct);
            }
            catch (Exception ex)
            {
                // Non-fatal: sending succeeded, only Sent folder copy had an issue
                logger?.Report($"[!] Note: Could not append to IMAP Sent folder: {ex.Message}");
            }
        }
    }
}
