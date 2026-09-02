using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Security;
using EmailSummarizer.Models;

namespace EmailSummarizer.Services
{
    public class OutlookTokenResult
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public int ExpiresIn { get; set; } = 3600;
        public string Email { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }

    public static class OutlookOAuthService
    {
        public const string ClientId = "050cc6f7-9900-48f5-9b78-e41523eb0218";
        public const string AuthorizeEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
        public const string TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
        public const string Scopes = "offline_access https://outlook.office.com/IMAP.AccessAsUser.All https://outlook.office.com/SMTP.Send openid profile email";

        public const string DefaultImapHost = "outlook.office365.com";
        public const int DefaultImapPort = 993;
        public const string DefaultSmtpHost = "smtp.office365.com";
        public const int DefaultSmtpPort = 587;

        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> AccountRefreshLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

        /// <summary>
        /// Performs the interactive OAuth 2.0 Authorization Code flow with PKCE using the user's default web browser
        /// and a local ephemeral loopback listener.
        /// </summary>
        public static async Task<(bool Success, string? ErrorMessage, OutlookTokenResult? Tokens)> SignInAsync(CancellationToken ct = default)
        {
            int port = GetRandomUnusedPort();
            string redirectUri = $"http://localhost:{port}/";

            using var listener = new HttpListener();
            try
            {
                listener.Prefixes.Add(redirectUri);
                listener.Start();
            }
            catch (Exception ex)
            {
                return (false, $"Could not start local callback listener: {ex.Message}", null);
            }

            string codeVerifier = GenerateRandomString(64);
            string codeChallenge = GenerateCodeChallenge(codeVerifier);
            string state = Guid.NewGuid().ToString("N");

            var queryParams = new Dictionary<string, string>
            {
                { "client_id", ClientId },
                { "response_type", "code" },
                { "redirect_uri", redirectUri },
                { "response_mode", "query" },
                { "scope", Scopes },
                { "state", state },
                { "code_challenge", codeChallenge },
                { "code_challenge_method", "S256" },
                { "prompt", "select_account" }
            };

            string authUrl = AuthorizeEndpoint + "?" + string.Join("&", queryParams.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

            try
            {
                Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                listener.Stop();
                return (false, $"Failed to launch system browser: {ex.Message}", null);
            }

            HttpListenerContext context;
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
                
                var getContextTask = listener.GetContextAsync();
                var completedTask = await Task.WhenAny(getContextTask, Task.Delay(Timeout.Infinite, linkedCts.Token));

                if (completedTask != getContextTask)
                {
                    listener.Stop();
                    return (false, "Sign-in was cancelled or timed out waiting for browser completion.", null);
                }

                context = await getContextTask;
            }
            catch (Exception ex)
            {
                listener.Stop();
                return (false, $"Sign-in listener error: {ex.Message}", null);
            }

            string? code = context.Request.QueryString["code"];
            string? returnedState = context.Request.QueryString["state"];
            string? error = context.Request.QueryString["error"];
            string? errorDescription = context.Request.QueryString["error_description"];

            // Send friendly response to the browser tab
            try
            {
                string responseHtml;
                if (!string.IsNullOrEmpty(code))
                {
                    responseHtml = @"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><title>Sign In Successful</title></head>
<body style='font-family:Segoe UI,sans-serif;text-align:center;padding:40px;background:#f5f7fa;'>
  <div style='max-width:440px;margin:auto;background:#fff;padding:32px;border-radius:10px;box-shadow:0 4px 16px rgba(0,0,0,0.08);'>
    <div style='font-size:44px;color:#28a745;margin-bottom:12px;'>✓</div>
    <h2 style='color:#1a1f36;margin:0 0 10px;'>Authentication Successful</h2>
    <p style='color:#4f566b;font-size:14px;line-height:1.5;'>You have connected your Outlook account to <strong>Email Summarizer</strong>. You can now close this browser window and return to the application.</p>
  </div>
</body>
</html>";
                }
                else
                {
                    responseHtml = $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><title>Sign In Error</title></head>
<body style='font-family:Segoe UI,sans-serif;text-align:center;padding:40px;background:#f5f7fa;'>
  <div style='max-width:440px;margin:auto;background:#fff;padding:32px;border-radius:10px;box-shadow:0 4px 16px rgba(0,0,0,0.08);'>
    <div style='font-size:44px;color:#d9534f;margin-bottom:12px;'>✕</div>
    <h2 style='color:#1a1f36;margin:0 0 10px;'>Authentication Failed</h2>
    <p style='color:#4f566b;font-size:14px;line-height:1.5;'>{WebUtility.HtmlEncode(errorDescription ?? error ?? "Unknown authorization error.")}</p>
  </div>
</body>
</html>";
                }

                byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
            catch { }
            finally
            {
                try { listener.Stop(); } catch { }
            }

            if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
            {
                return (false, $"Microsoft Sign-In error: {errorDescription ?? error ?? "No authorization code returned."}", null);
            }

            if (!string.Equals(state, returnedState, StringComparison.Ordinal))
            {
                return (false, "State parameter mismatch (possible CSRF rejection).", null);
            }

            // Exchange Authorization Code for Tokens
            return await ExchangeCodeForTokensAsync(code, redirectUri, codeVerifier, ct);
        }

        private static async Task<(bool Success, string? ErrorMessage, OutlookTokenResult? Tokens)> ExchangeCodeForTokensAsync(
            string code,
            string redirectUri,
            string codeVerifier,
            CancellationToken ct)
        {
            try
            {
                var formValues = new Dictionary<string, string>
                {
                    { "client_id", ClientId },
                    { "grant_type", "authorization_code" },
                    { "code", code },
                    { "redirect_uri", redirectUri },
                    { "code_verifier", codeVerifier },
                    { "scope", Scopes }
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(formValues)
                };

                using var res = await HttpClient.SendAsync(req, ct);
                string json = await res.Content.ReadAsStringAsync(ct);

                if (!res.IsSuccessStatusCode)
                {
                    return (false, $"Token exchange failed ({res.StatusCode}): {json}", null);
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string accessToken = root.GetProperty("access_token").GetString() ?? "";
                string refreshToken = root.TryGetProperty("refresh_token", out var rt) ? (rt.GetString() ?? "") : "";
                int expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

                string idToken = root.TryGetProperty("id_token", out var idt) ? (idt.GetString() ?? "") : "";
                var userInfo = ExtractUserFromIdToken(idToken);

                string email = userInfo.Email ?? "";
                string displayName = userInfo.DisplayName ?? (!string.IsNullOrEmpty(email) ? $"Outlook ({email})" : "Outlook Account");

                if (string.IsNullOrEmpty(email))
                {
                    // Fallback query to Microsoft Graph /v1.0/me to retrieve email
                    var graphUser = await FetchUserFromGraphAsync(accessToken, ct);
                    if (!string.IsNullOrEmpty(graphUser.Email))
                    {
                        email = graphUser.Email;
                        if (!string.IsNullOrEmpty(graphUser.DisplayName)) displayName = graphUser.DisplayName;
                    }
                }

                if (string.IsNullOrEmpty(email))
                {
                    return (false, "Could not determine email address from Microsoft identity token.", null);
                }

                var tokens = new OutlookTokenResult
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresIn = expiresIn,
                    Email = email,
                    DisplayName = displayName
                };

                return (true, null, tokens);
            }
            catch (Exception ex)
            {
                return (false, $"Token exchange exception: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Ensures the account has an active, valid OAuth access token.
        /// Checks if more than 45 minutes have elapsed since LastRefreshedUtc or if expiring soon.
        /// If fresh, returns the decrypted existing token with zero latency (keeping multi-account parallel fetch fast).
        /// If stale (>45 min), requests a fresh token, updates accounts.dat, and returns the new token.
        /// </summary>
        public static async Task<string?> EnsureValidAccessTokenAsync(
            EmailAccount account,
            ConfigService? configService = null,
            IProgress<string>? logger = null,
            CancellationToken ct = default)
        {
            if (!account.IsOutlookOAuth)
            {
                return null;
            }

            string decryptedAccessToken = AccountCryptoService.DecryptString(account.EncryptedAccessToken);
            string decryptedRefreshToken = AccountCryptoService.DecryptString(account.EncryptedRefreshToken);

            bool needsRefresh = false;

            // 1. Missing access token
            if (string.IsNullOrWhiteSpace(decryptedAccessToken))
            {
                needsRefresh = true;
            }
            // 2. User rule: Check if it has been at least 45 minutes since last refresh
            else if (account.LastRefreshedUtc.HasValue && (DateTime.UtcNow - account.LastRefreshedUtc.Value).TotalMinutes >= 45)
            {
                needsRefresh = true;
            }
            // 3. User rule: Check if accessTokenExpiresUtc has expired or expires within 15 minutes
            else if (account.AccessTokenExpiresUtc.HasValue && account.AccessTokenExpiresUtc.Value <= DateTime.UtcNow.AddMinutes(15))
            {
                needsRefresh = true;
            }
            // 4. Missing LastRefreshedUtc timestamp
            else if (!account.LastRefreshedUtc.HasValue)
            {
                needsRefresh = true;
            }

            if (!needsRefresh)
            {
                // Token is fresh (<45 minutes old) -> proceed immediately with zero network overhead
                return decryptedAccessToken;
            }

            if (string.IsNullOrWhiteSpace(decryptedRefreshToken))
            {
                logger?.Report($"[!] {account.Name}: No refresh token available. User must re-authenticate.");
                return !string.IsNullOrWhiteSpace(decryptedAccessToken) ? decryptedAccessToken : null;
            }

            // Synchronize per account so parallel fetching doesn't duplicate refresh requests for the same account
            var accountLock = AccountRefreshLocks.GetOrAdd(account.Id, _ => new SemaphoreSlim(1, 1));
            await accountLock.WaitAsync(ct);

            try
            {
                // Re-check condition in case another thread already completed the refresh
                if (account.LastRefreshedUtc.HasValue && (DateTime.UtcNow - account.LastRefreshedUtc.Value).TotalMinutes < 45 && !string.IsNullOrWhiteSpace(account.EncryptedAccessToken))
                {
                    return AccountCryptoService.DecryptString(account.EncryptedAccessToken);
                }

                logger?.Report($"[*] {account.Name}: Refreshing Outlook OAuth access token (>45m elapsed)...");

                var formValues = new Dictionary<string, string>
                {
                    { "client_id", ClientId },
                    { "grant_type", "refresh_token" },
                    { "refresh_token", decryptedRefreshToken },
                    { "scope", Scopes }
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(formValues)
                };

                using var res = await HttpClient.SendAsync(req, ct);
                string json = await res.Content.ReadAsStringAsync(ct);

                if (!res.IsSuccessStatusCode)
                {
                    logger?.Report($"[!] {account.Name}: Failed to refresh Outlook token ({res.StatusCode}): {json}");
                    // Fall back to existing token if still valid
                    if (!string.IsNullOrWhiteSpace(decryptedAccessToken) && account.AccessTokenExpiresUtc.HasValue && account.AccessTokenExpiresUtc.Value > DateTime.UtcNow)
                    {
                        return decryptedAccessToken;
                    }
                    return null;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string newAccessToken = root.GetProperty("access_token").GetString() ?? "";
                string newRefreshToken = root.TryGetProperty("refresh_token", out var rt) ? (rt.GetString() ?? "") : "";
                int expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

                account.EncryptedAccessToken = AccountCryptoService.EncryptString(newAccessToken);
                if (!string.IsNullOrWhiteSpace(newRefreshToken))
                {
                    account.EncryptedRefreshToken = AccountCryptoService.EncryptString(newRefreshToken);
                }
                account.AccessTokenExpiresUtc = DateTime.UtcNow.AddSeconds(expiresIn);
                account.LastRefreshedUtc = DateTime.UtcNow;

                // Atomically persist refreshed tokens to accounts.dat
                PersistAccountTokenUpdate(account, configService);

                logger?.Report($"[✓] {account.Name}: Outlook access token successfully refreshed.");
                return newAccessToken;
            }
            catch (Exception ex)
            {
                logger?.Report($"[!] {account.Name}: Error refreshing Outlook token: {ex.Message}");
                return !string.IsNullOrWhiteSpace(decryptedAccessToken) ? decryptedAccessToken : null;
            }
            finally
            {
                accountLock.Release();
            }
        }

        /// <summary>
        /// Unified authentication helper for MailKit IMailService (both ImapClient and SmtpClient).
        /// Authenticates using OAuth2 (SaslMechanismOAuth2) for Outlook accounts, or username/password for standard accounts.
        /// </summary>
        public static async Task AuthenticateMailServiceAsync(
            IMailService client,
            EmailAccount account,
            ConfigService? configService = null,
            IProgress<string>? logger = null,
            CancellationToken ct = default)
        {
            if (account.IsOutlookOAuth)
            {
                string? token = await EnsureValidAccessTokenAsync(account, configService, logger, ct);
                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new InvalidOperationException($"Failed to acquire valid Outlook OAuth access token for {account.Email}. Please re-authenticate the account.");
                }

                var oauth2 = new SaslMechanismOAuth2(account.Email, token);
                await client.AuthenticateAsync(oauth2, ct);
            }
            else
            {
                await client.AuthenticateAsync(account.Email, account.AppPassword, ct);
            }
        }

        private static void PersistAccountTokenUpdate(EmailAccount account, ConfigService? configService)
        {
            try
            {
                if (configService != null)
                {
                    var allAccounts = configService.GetAccounts();
                    var match = allAccounts.FirstOrDefault(a => a.Id == account.Id || string.Equals(a.Email, account.Email, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        match.EncryptedAccessToken = account.EncryptedAccessToken;
                        match.EncryptedRefreshToken = account.EncryptedRefreshToken;
                        match.AccessTokenExpiresUtc = account.AccessTokenExpiresUtc;
                        match.LastRefreshedUtc = account.LastRefreshedUtc;
                        configService.SaveAccounts(allAccounts);
                        return;
                    }
                }

                // If configService is not passed (e.g. background daemon), save directly to accounts.dat
                string path = ConfigService.AccountsFilePath;
                if (File.Exists(path))
                {
                    var accounts = AccountCryptoService.LoadFromEncryptedFile(path);
                    var match = accounts.FirstOrDefault(a => a.Id == account.Id || string.Equals(a.Email, account.Email, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        match.EncryptedAccessToken = account.EncryptedAccessToken;
                        match.EncryptedRefreshToken = account.EncryptedRefreshToken;
                        match.AccessTokenExpiresUtc = account.AccessTokenExpiresUtc;
                        match.LastRefreshedUtc = account.LastRefreshedUtc;
                        AccountCryptoService.SaveToEncryptedFile(path, accounts);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OutlookOAuthService] PersistAccountTokenUpdate error: {ex.Message}");
            }
        }

        private static (string? Email, string? DisplayName) ExtractUserFromIdToken(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken)) return (null, null);

            try
            {
                var parts = idToken.Split('.');
                if (parts.Length >= 2)
                {
                    string payload = parts[1];
                    payload = payload.Replace('-', '+').Replace('_', '/');
                    switch (payload.Length % 4)
                    {
                        case 2: payload += "=="; break;
                        case 3: payload += "="; break;
                    }
                    byte[] bytes = Convert.FromBase64String(payload);
                    using var doc = JsonDocument.Parse(bytes);
                    var root = doc.RootElement;

                    string? email = null;
                    if (root.TryGetProperty("email", out var el) && !string.IsNullOrWhiteSpace(el.GetString()))
                        email = el.GetString();
                    else if (root.TryGetProperty("preferred_username", out var upn) && !string.IsNullOrWhiteSpace(upn.GetString()))
                        email = upn.GetString();
                    else if (root.TryGetProperty("upn", out var upn2) && !string.IsNullOrWhiteSpace(upn2.GetString()))
                        email = upn2.GetString();

                    string? name = null;
                    if (root.TryGetProperty("name", out var n) && !string.IsNullOrWhiteSpace(n.GetString()))
                        name = n.GetString();

                    return (email, name);
                }
            }
            catch { }

            return (null, null);
        }

        private static async Task<(string? Email, string? DisplayName)> FetchUserFromGraphAsync(string accessToken, CancellationToken ct)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                using var res = await HttpClient.SendAsync(req, ct);
                if (res.IsSuccessStatusCode)
                {
                    string json = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    string? email = root.TryGetProperty("mail", out var m) ? m.GetString() : null;
                    if (string.IsNullOrEmpty(email) && root.TryGetProperty("userPrincipalName", out var upn))
                    {
                        email = upn.GetString();
                    }
                    string? name = root.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;

                    return (email, name);
                }
            }
            catch { }

            return (null, null);
        }

        private static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string GenerateRandomString(int length)
        {
            byte[] bytes = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=')
                .Substring(0, length);
        }

        private static string GenerateCodeChallenge(string codeVerifier)
        {
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
            return Convert.ToBase64String(hash)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
