using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using KerkenezMail.Models;

namespace KerkenezMail.Services
{
    public class UnreadNotificationInfo
    {
        public string AccountName { get; set; } = "";
        public string Sender { get; set; } = "";
        public string Subject { get; set; } = "";
        public DateTimeOffset Date { get; set; }
    }

    public class TrayDaemonService : IDisposable
    {
        private readonly ConfigService _configService;
        private readonly HashSet<string> _seenUnreadUids = new HashSet<string>();
        private readonly SemaphoreSlim _checkLock = new SemaphoreSlim(1, 1);
        private System.Threading.Timer? _pollTimer;
        private CancellationTokenSource? _cts;
        private bool _isDisposed;

        public event Action<int, string>? UnreadStatusUpdated;
        public event Action<List<UnreadNotificationInfo>>? NewUnreadEmailsDiscovered;

        public int CurrentUnreadCount { get; private set; } = 0;
        public bool IsChecking { get; private set; }

        public TrayDaemonService(ConfigService? configService = null)
        {
            _configService = configService ?? new ConfigService();
        }

        public void Start()
        {
            if (_isDisposed) return;

            _cts = new CancellationTokenSource();
            ScheduleNextPoll(TimeSpan.FromSeconds(2)); // Initial check shortly after startup
        }

        public void Stop()
        {
            _cts?.Cancel();
            _pollTimer?.Dispose();
            _pollTimer = null;
        }

        public void RestartWithNewInterval()
        {
            Stop();
            Start();
        }

        private void ScheduleNextPoll(TimeSpan delay)
        {
            if (_isDisposed) return;

            _pollTimer?.Dispose();
            _pollTimer = new System.Threading.Timer(async _ =>
            {
                await RunCheckAsync();
                
                // Reload config to get dynamic interval updates
                _configService.LoadConfig();
                int minutes = Math.Max(1, _configService.Settings.TrayRefreshIntervalMinutes);
                ScheduleNextPoll(TimeSpan.FromMinutes(minutes));
            }, null, (int)delay.TotalMilliseconds, Timeout.Infinite);
        }

        public async Task TriggerCheckNowAsync()
        {
            await RunCheckAsync();
        }

        public async Task RunCheckAsync()
        {
            if (!await _checkLock.WaitAsync(0)) return; // Prevent concurrent overlap

            try
            {
                IsChecking = true;
                _configService.LoadConfig();
                var settings = _configService.Settings;
                var accounts = _configService.GetAccounts().Where(a => a.IsEnabled).ToList();

                if (accounts.Count == 0)
                {
                    CurrentUnreadCount = 0;
                    UnreadStatusUpdated?.Invoke(0, "No accounts configured or enabled");
                    return;
                }

                int totalUnread = 0;
                var newNotifications = new List<UnreadNotificationInfo>();
                var currentCycleSeenUids = new HashSet<string>();

                var checkTasks = accounts.Select(async account =>
                {
                    return await CheckAccountUnreadAsync(account, settings.EnableTrayNotifications, _cts?.Token ?? CancellationToken.None);
                });

                var results = await Task.WhenAll(checkTasks);

                foreach (var res in results)
                {
                    totalUnread += res.UnreadCount;
                    foreach (var uidKey in res.FoundUids)
                    {
                        currentCycleSeenUids.Add(uidKey);
                    }
                    newNotifications.AddRange(res.NewNotifications);
                }

                // Clean up stale UIDs from tracker that are no longer unread
                _seenUnreadUids.RemoveWhere(uid => !currentCycleSeenUids.Contains(uid));

                CurrentUnreadCount = totalUnread;

                string status = totalUnread == 0
                    ? "All caught up (0 unread)"
                    : $"{totalUnread} unread email{(totalUnread > 1 ? "s" : "")}";

                UnreadStatusUpdated?.Invoke(totalUnread, status);

                if (settings.EnableTrayNotifications && newNotifications.Count > 0)
                {
                    NewUnreadEmailsDiscovered?.Invoke(newNotifications);
                }
            }
            catch (Exception ex)
            {
                UnreadStatusUpdated?.Invoke(CurrentUnreadCount, $"Check error: {ex.Message}");
            }
            finally
            {
                IsChecking = false;
                _checkLock.Release();

                // Ultra-aggressive memory compaction to maintain sub-5MB idle footprint
                NativeMethods.TrimWorkingSet();
            }
        }

        private async Task<(int UnreadCount, List<string> FoundUids, List<UnreadNotificationInfo> NewNotifications)> CheckAccountUnreadAsync(
            EmailAccount account,
            bool fetchHeadersForNotification,
            CancellationToken ct)
        {
            var foundUids = new List<string>();
            var newNotifs = new List<UnreadNotificationInfo>();
            int unreadCount = 0;

            using var client = new ImapClient();
            try
            {
                client.Timeout = 10000;
                var sslOption = account.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;

                await client.ConnectAsync(account.Host, account.Port, sslOption, ct);
                await OutlookOAuthService.AuthenticateMailServiceAsync(client, account, _configService, ct: ct);

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

                var unreadUids = await inbox.SearchAsync(SearchQuery.NotSeen, ct);
                unreadCount = unreadUids.Count;

                var newUidsToFetch = new List<UniqueId>();

                foreach (var uid in unreadUids)
                {
                    string uidKey = $"{account.Id}:{uid.Id}";
                    foundUids.Add(uidKey);

                    if (!_seenUnreadUids.Contains(uidKey))
                    {
                        _seenUnreadUids.Add(uidKey);
                        if (fetchHeadersForNotification)
                        {
                            newUidsToFetch.Add(uid);
                        }
                    }
                }

                // If notifications are enabled, fetch lightweight Envelopes (Subject + From) only for newly discovered unread emails
                if (fetchHeadersForNotification && newUidsToFetch.Count > 0)
                {
                    // Limit notification headers batch to 5 to avoid memory surge
                    var targetUids = newUidsToFetch.Take(5).ToList();
                    var summaries = await inbox.FetchAsync(targetUids, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId, ct);

                    foreach (var s in summaries)
                    {
                        string sender = s.Envelope?.From?.Mailboxes?.FirstOrDefault()?.ToString() 
                                     ?? s.Envelope?.From?.ToString() 
                                     ?? "(Unknown Sender)";
                        string subject = string.IsNullOrWhiteSpace(s.Envelope?.Subject) 
                                     ? "(No Subject)" 
                                     : s.Envelope.Subject;

                        newNotifs.Add(new UnreadNotificationInfo
                        {
                            AccountName = account.Name,
                            Sender = sender,
                            Subject = subject,
                            Date = s.Envelope?.Date ?? DateTimeOffset.Now
                        });
                    }
                }

                await client.DisconnectAsync(true, ct);
            }
            catch
            {
                // Silently handle transient connection errors in daemon polling
            }

            return (unreadCount, foundUids, newNotifs);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
            _checkLock.Dispose();
        }
    }
}
