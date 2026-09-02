using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using EmailSummarizer.Models;

namespace EmailSummarizer.Services
{
    public class LiveImapService : IDisposable
    {
        private readonly ConfigService _configService;
        private readonly IProgress<string>? _logger;
        private CancellationTokenSource? _masterCts;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeDoneSources = new();
        private readonly ConcurrentDictionary<string, ImapClient> _activeClients = new();
        private readonly List<Task> _workerTasks = new();
        private readonly SemaphoreSlim _stateLock = new(1, 1);
        private bool _isDisposed = false;

        public event Action<EmailAccount, int>? NewEmailDetected;
        public event Action<bool>? StatusChanged;

        public bool IsRunning { get; private set; } = false;

        public LiveImapService(ConfigService configService, IProgress<string>? logger = null)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger;
        }

        public async Task StartAsync()
        {
            await _stateLock.WaitAsync();
            try
            {
                if (IsRunning || _isDisposed) return;

                var accounts = _configService.GetAccounts().Where(a => a.IsEnabled).ToList();
                if (accounts.Count == 0)
                {
                    _logger?.Report("[Live IMAP] No enabled email accounts found to monitor.");
                    return;
                }

                _masterCts = new CancellationTokenSource();
                _workerTasks.Clear();
                _activeDoneSources.Clear();
                _activeClients.Clear();
                IsRunning = true;
                StatusChanged?.Invoke(true);

                _logger?.Report($"\r\n[Live IMAP] Starting Live IMAP idle listener for {accounts.Count} account(s)...");

                foreach (var account in accounts)
                {
                    var task = Task.Run(() => RunAccountIdleLoopAsync(account, _masterCts.Token));
                    _workerTasks.Add(task);
                }
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public async Task StopAsync()
        {
            await _stateLock.WaitAsync();
            try
            {
                if (!IsRunning) return;
                IsRunning = false;
                StatusChanged?.Invoke(false);

                _logger?.Report("[Live IMAP] Stopping Live IMAP listener. Transmitting DONE signals to servers...");

                // 1. Trigger the DONE signal for every currently idling IMAP client
                foreach (var kvp in _activeDoneSources)
                {
                    try
                    {
                        var accountId = kvp.Key;
                        var doneCts = kvp.Value;
                        if (!doneCts.IsCancellationRequested)
                        {
                            doneCts.Cancel();
                        }
                    }
                    catch { }
                }

                // 2. Cancel master token for any waiting delay tasks
                try
                {
                    _masterCts?.Cancel();
                }
                catch { }

                // 3. Await all worker tasks with a reasonable timeout so we never hang
                if (_workerTasks.Count > 0)
                {
                    try
                    {
                        await Task.WhenAny(Task.WhenAll(_workerTasks), Task.Delay(4000));
                    }
                    catch { }
                }

                // 4. Gracefully disconnect and dispose all clients
                foreach (var kvp in _activeClients)
                {
                    try
                    {
                        var client = kvp.Value;
                        if (client.IsConnected)
                        {
                            await client.DisconnectAsync(true);
                        }
                        client.Dispose();
                    }
                    catch { }
                }

                _activeClients.Clear();
                _activeDoneSources.Clear();
                _workerTasks.Clear();

                try
                {
                    _masterCts?.Dispose();
                    _masterCts = null;
                }
                catch { }

                _logger?.Report("[Live IMAP] All idle connections terminated cleanly with DONE signal.");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        private async Task RunAccountIdleLoopAsync(EmailAccount account, CancellationToken masterToken)
        {
            while (!masterToken.IsCancellationRequested && IsRunning)
            {
                ImapClient? client = null;
                try
                {
                    client = new ImapClient
                    {
                        Timeout = 30000
                    };
                    _activeClients[account.Id] = client;

                    var sslOption = account.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;
                    _logger?.Report($"[Live IMAP] {account.Name}: Connecting to {account.Host}:{account.Port}...");
                    await client.ConnectAsync(account.Host, account.Port, sslOption, masterToken);

                    _logger?.Report($"[Live IMAP] {account.Name}: Authenticating...");
                    await OutlookOAuthService.AuthenticateMailServiceAsync(client, account, null, _logger, masterToken);

                    var inbox = client.Inbox;
                    await inbox.OpenAsync(FolderAccess.ReadOnly, masterToken);

                    bool supportsIdle = client.Capabilities.HasFlag(ImapCapabilities.Idle);
                    _logger?.Report($"[Live IMAP] {account.Name}: Inbox opened ({inbox.Count} message(s)). IDLE support: {(supportsIdle ? "Yes" : "No (fallback mode)")}.");

                    int previousCount = inbox.Count;

                    // Attach change listeners
                    void OnCountChanged(object? s, EventArgs e)
                    {
                        if (inbox.Count > previousCount)
                        {
                            int diff = inbox.Count - previousCount;
                            _logger?.Report($"[Live IMAP] {account.Name}: {diff} new email(s) arrived in Inbox!");
                            NewEmailDetected?.Invoke(account, diff);
                        }
                        previousCount = inbox.Count;
                    }

                    inbox.CountChanged += OnCountChanged;

                    try
                    {
                        // Inner loop for IDLE renewals (RFC 2177 recommends renewing IDLE at least every 29 mins).
                        // IMAP servers (especially Gmail and Exchange/Outlook) silently drop dead idle sockets without
                        // sending a FIN packet if no traffic passes for 20–29 minutes.
                        // We cancel IDLE every 15 minutes, transmit DONE, fire a lightweight NOOP to keep NAT state
                        // tables and server-side timers alive, and then re-enter IDLE.
                        while (!masterToken.IsCancellationRequested && IsRunning && client.IsConnected && client.IsAuthenticated)
                        {
                            if (supportsIdle)
                            {
                                // Set up a 15-minute renewal token
                                using var doneCts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
                                _activeDoneSources[account.Id] = doneCts;

                                try
                                {
                                    // IdleAsync will send "DONE\r\n" when doneCts is cancelled
                                    await client.IdleAsync(doneCts.Token, masterToken);
                                }
                                catch (OperationCanceledException) when (doneCts.IsCancellationRequested && !masterToken.IsCancellationRequested)
                                {
                                    // Periodic 15-minute timeout or manual stop
                                }
                                finally
                                {
                                    _activeDoneSources.TryRemove(account.Id, out _);
                                }

                                // If Live IMAP is still actively running and app is open, fire lightweight NOOP
                                // to refresh NAT translation tables, intermediate firewalls, and server inactivity timers
                                if (IsRunning && !masterToken.IsCancellationRequested && client.IsConnected && client.IsAuthenticated)
                                {
                                    _logger?.Report($"[Live IMAP] {account.Name}: 15-min timeout reached. Sent DONE, firing NOOP keep-alive...");
                                    await client.NoOpAsync(masterToken);
                                    _logger?.Report($"[Live IMAP] {account.Name}: Keep-alive confirmed. Re-entering IDLE.");
                                }
                            }
                            else
                            {
                                // Fallback periodic NOOP for rare servers lacking IDLE capability
                                await Task.Delay(TimeSpan.FromSeconds(60), masterToken);
                                if (!masterToken.IsCancellationRequested && client.IsConnected)
                                {
                                    await client.NoOpAsync(masterToken);
                                }
                            }
                        }
                    }
                    finally
                    {
                        inbox.CountChanged -= OnCountChanged;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Clean cancellation requested
                    break;
                }
                catch (Exception ex)
                {
                    if (masterToken.IsCancellationRequested || !IsRunning) break;
                    _logger?.Report($"[Live IMAP] {account.Name} error: {ex.Message}. Reconnecting in 15 seconds...");
                    try
                    {
                        await Task.Delay(15000, masterToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                finally
                {
                    _activeDoneSources.TryRemove(account.Id, out _);
                    if (client != null)
                    {
                        try
                        {
                            if (client.IsConnected)
                            {
                                await client.DisconnectAsync(true);
                            }
                        }
                        catch { }
                        finally
                        {
                            client.Dispose();
                            _activeClients.TryRemove(account.Id, out _);
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                // Synchronous cleanup fallback if StopAsync was not called
                if (IsRunning)
                {
                    _masterCts?.Cancel();
                    foreach (var kvp in _activeDoneSources)
                    {
                        try { kvp.Value.Cancel(); } catch { }
                    }
                    foreach (var kvp in _activeClients)
                    {
                        try { kvp.Value.Dispose(); } catch { }
                    }
                }
            }
            catch { }
            finally
            {
                _stateLock.Dispose();
            }
        }
    }
}
