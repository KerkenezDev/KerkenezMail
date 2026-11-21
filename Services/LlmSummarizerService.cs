using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KerkenezMail.Languages;
using KerkenezMail.Models;

namespace KerkenezMail.Services
{
    public sealed class AskMoreStreamResult
    {
        public bool Success { get; init; }
        public string Content { get; init; } = string.Empty;
        public bool WasStopped { get; init; }
    }

    public class LlmSummarizerService
    {
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

        public static string PrepareEmailBodyForSummary(string fullBody, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(fullBody))
                return "(No text content in this email)";

            // 0 or negative indicates Unlimited (send entire email body)
            if (maxChars <= 0 || fullBody.Length <= maxChars)
                return fullBody;

            return fullBody.Substring(0, maxChars) + "\r\n\r\n... [Remaining email content truncated for AI context length limit]";
        }

        private static string GetEmailContextNotice(EmailItem email)
        {
            bool isThreadOrReply = email.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) ||
                                   email.Subject.StartsWith("Fwd:", StringComparison.OrdinalIgnoreCase) ||
                                   (email.Subject.Contains("[") && email.Subject.Contains("]"));

            bool hasNewsletterSignature = email.HasNewsletterFooter || 
                                          ImapService.DetectNewsletterFooter(email.RawBody) || 
                                          ImapService.DetectNewsletterFooter(email.CleanBody);

            if (email.IsMailingList && hasNewsletterSignature && !isThreadOrReply)
            {
                return "Context Note: Bulk mailing list / newsletter detected (suggested Priority: 3).\r\n";
            }
            else if (isThreadOrReply)
            {
                return "Context Note: Direct reply or discussion thread.\r\n";
            }
            else if (email.IsMailingList)
            {
                return "Context Note: Automated notification or mailing list.\r\n";
            }

            return string.Empty;
        }

        private static bool ValidateLlamaModelPath(AppSettings settings, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!string.Equals(settings.AiBackend, "LlamaCpp", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(settings.LlamaModelPath))
            {
                errorMessage = "No GGUF model selected. Go to Settings > AI Backend to select a model.";
                return false;
            }

            if (!File.Exists(settings.LlamaModelPath))
            {
                string fileName = Path.GetFileName(settings.LlamaModelPath);
                errorMessage = string.IsNullOrWhiteSpace(fileName)
                    ? "Model file not found. Please verify the GGUF model path in Settings."
                    : $"Model file not found ({fileName}). Please verify the GGUF model path in Settings.";
                return false;
            }

            return true;
        }

        public static string BuildEmailLinksContext(EmailItem email)
        {
            if (email == null) return string.Empty;

            var entries = new List<(string Label, string Url, int Quality, int Order)>();
            var indexByUrl = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            void TryAddLink(string? rawLabel, string? rawUrl)
            {
                if (string.IsNullOrWhiteSpace(rawUrl)) return;
                string url = rawUrl.Trim();
                if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }

                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeMailto))
                {
                    return;
                }

                string cleanLabel = Regex.Replace((rawLabel ?? string.Empty).Trim(), @"\s+", " ");
                int quality;
                if (string.IsNullOrWhiteSpace(cleanLabel) ||
                    string.Equals(cleanLabel, "[Remote Content]", StringComparison.OrdinalIgnoreCase))
                {
                    quality = 0;
                    cleanLabel = string.IsNullOrWhiteSpace(uri.Host) ? url : uri.Host;
                }
                else if (cleanLabel.StartsWith("[Remote Content:", StringComparison.OrdinalIgnoreCase) ||
                         cleanLabel.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         cleanLabel.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                         cleanLabel.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                {
                    quality = 1;
                }
                else
                {
                    quality = 2;
                }

                cleanLabel = cleanLabel.Replace("[", "(").Replace("]", ")");
                if (cleanLabel.Length > 100)
                {
                    cleanLabel = cleanLabel.Substring(0, 97).TrimEnd() + "...";
                }

                if (indexByUrl.TryGetValue(url, out int existingIdx))
                {
                    if (quality > entries[existingIdx].Quality)
                    {
                        entries[existingIdx] = (cleanLabel, url, quality, entries[existingIdx].Order);
                    }
                    return;
                }

                indexByUrl[url] = entries.Count;
                entries.Add((cleanLabel, url, quality, entries.Count));
            }

            if (email.ExtractedLinks != null)
            {
                foreach (var link in email.ExtractedLinks)
                {
                    TryAddLink(link.Text, link.Url);
                }
            }

            string bodySource = !string.IsNullOrWhiteSpace(email.DisplayBody) ? email.DisplayBody : email.CleanBody;
            if (!string.IsNullOrWhiteSpace(bodySource))
            {
                var urlMatches = Regex.Matches(bodySource, @"https?://[^\s<>""'{}|\^\[\]`]+", RegexOptions.IgnoreCase);
                foreach (Match m in urlMatches)
                {
                    string trimmed = m.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')');
                    TryAddLink(null, trimmed);
                }
            }

            if (entries.Count == 0) return string.Empty;

            var ordered = entries
                .OrderByDescending(e => e.Quality)
                .ThenBy(e => e.Order)
                .Take(50);

            var sb = new StringBuilder();
            const int maxContextChars = 4000;
            foreach (var entry in ordered)
            {
                string line = $"- [{entry.Label}]({entry.Url})";
                if (sb.Length > 0 && sb.Length + line.Length + 2 > maxContextChars)
                {
                    break;
                }
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(line);
            }

            return sb.ToString();
        }

        public async Task<string> SummarizeEmailAsync(
            EmailItem email,
            AppSettings settings,
            CancellationToken ct = default)
        {
            if (settings.IsAiDisabled)
            {
                return string.Empty;
            }

            if (!ValidateLlamaModelPath(settings, out string modelError))
            {
                email.Priority = 2;
                return modelError;
            }

            string emailContentForLlm = PrepareEmailBodyForSummary(email.CleanBody, settings.MaxSummaryEmailChars);
            string metadataNotice = GetEmailContextNotice(email);

            var userContent = $"Subject: {email.Subject}\r\n" +
                              $"From: {email.Sender}\r\n" +
                              metadataNotice +
                              $"Email Content:\r\n{emailContentForLlm}\r\n\r\n" +
                              $"Read the email above. First write a concise summary (1-3 sentences), then assign the Priority rank (1 = Critical/Urgent emergency ONLY, 2 = Normal/Default for routine emails, 3 = Low/Newsletters/Marketing).\r\n" +
                              $"Do NOT output scratchpad notes, drafts, or numbered analysis steps.\r\n" +
                              $"Output strictly in this format:\r\n" +
                              $"Summary: <1-3 sentence brief>\r\n" +
                              $"Priority: <1, 2, or 3>";

            string systemPrompt = settings.SystemPrompt;
            if (!systemPrompt.Contains("Priority", StringComparison.OrdinalIgnoreCase))
            {
                systemPrompt += "\r\n\r\nPriority Guidelines (Rank from 1 to 3):\r\n" +
                                "* Priority 2 (Normal - DEFAULT for most emails): Standard work correspondence, routine questions, meeting invites, invoices, and updates.\r\n" +
                                "* Priority 1 (High / Urgent ONLY): Severe emergencies, production outages, broken CI/CD builds, critical security failures, or urgent same-day deadlines.\r\n" +
                                "* Priority 3 (Low / Newsletters / Bulk): Marketing promos, sales discounts, newsletters, and automated bulk notifications.\r\n" +
                                "* Rule: When in doubt between 1 and 2, ALWAYS default to 2.\r\n\r\n" +
                                "Required Output Format:\r\n" +
                                "Summary: <1-3 sentence brief>\r\n" +
                                "Priority: <1, 2, or 3>";
            }

            // Strict user preference: respect configured token limit without forced overrides
            int tokenBudget = settings.MaxTokens > 0 ? settings.MaxTokens : 350;

            var (success, rawOrError) = await SendChatCompletionAsync(
                settings,
                systemPrompt,
                userContent,
                settings.Temperature,
                tokenBudget,
                "(Summarization cancelled)",
                "(No summary content found in LLM response)",
                ct);

            if (!success)
            {
                email.Priority = 2;
                return rawOrError;
            }

            var (cleanSummary, parsedPriority) = ParseLlmSummaryAndPriority(rawOrError);
            email.Priority = parsedPriority;
            return cleanSummary;
        }

        private static string BuildEmailPromptBlock(EmailItem email, AppSettings settings)
        {
            string bodySource = !string.IsNullOrWhiteSpace(email.DisplayBody) ? email.DisplayBody : email.CleanBody;
            string emailContentForLlm = PrepareEmailBodyForSummary(bodySource, settings.MaxSummaryEmailChars);
            string linksContext = BuildEmailLinksContext(email);

            var sb = new StringBuilder();
            sb.AppendLine("Email Details:");
            sb.AppendLine($"Subject: {email.Subject}");
            sb.AppendLine($"From: {email.Sender}");
            sb.AppendLine($"Date: {email.DateString}");
            sb.AppendLine($"Account: {email.AccountName}");

            if (email.HasAttachments)
            {
                string attList = string.Join(", ", email.Attachments.Select(a => $"{a.FileName} ({a.FormattedSize})"));
                sb.AppendLine($"Attachments: {attList}");
            }

            sb.AppendLine();
            sb.AppendLine("Email Content:");
            sb.AppendLine(emailContentForLlm);
            sb.AppendLine();
            sb.AppendLine("Links in this email:");
            sb.Append(string.IsNullOrWhiteSpace(linksContext) ? "(None)" : linksContext);
            return sb.ToString();
        }

        public async Task<string> AskAboutEmailAsync(
            EmailItem email,
            string userQuestion,
            AppSettings settings,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userQuestion))
            {
                return string.Empty;
            }

            var result = await AskAboutEmailChatStreamAsync(
                email,
                new[] { new AskMoreTurn { Question = userQuestion } },
                settings,
                onVisibleTextUpdated: null,
                ct);

            return result.Content;
        }

        public async Task<AskMoreStreamResult> AskAboutEmailChatStreamAsync(
            EmailItem email,
            IReadOnlyList<AskMoreTurn> turns,
            AppSettings settings,
            Action<string>? onVisibleTextUpdated = null,
            CancellationToken ct = default)
        {
            if (settings.IsAiDisabled)
            {
                return new AskMoreStreamResult { Success = false, Content = Lang.T(StringKeys.AskMoreAiDisabled) };
            }

            if (turns == null || turns.Count == 0 || string.IsNullOrWhiteSpace(turns[turns.Count - 1].Question))
            {
                return new AskMoreStreamResult { Success = false, Content = string.Empty };
            }

            if (!ValidateLlamaModelPath(settings, out string modelError))
            {
                return new AskMoreStreamResult { Success = false, Content = modelError };
            }

            string systemPrompt = !string.IsNullOrWhiteSpace(settings.AskMoreSystemPrompt)
                ? settings.AskMoreSystemPrompt
                : AppSettings.CreateDefault().AskMoreSystemPrompt;

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            string emailBlock = BuildEmailPromptBlock(email, settings);

            for (int i = 0; i < turns.Count; i++)
            {
                var turn = turns[i];
                string q = (turn.Question ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(q)) continue;

                string userContent = i == 0
                    ? $"{emailBlock}\r\n\r\nUser Question:\r\n{q}"
                    : q;
                messages.Add(new { role = "user", content = userContent });

                bool isFinalTurn = (i == turns.Count - 1);
                if (!isFinalTurn && !turn.IsError && !string.IsNullOrWhiteSpace(turn.Answer))
                {
                    messages.Add(new { role = "assistant", content = turn.Answer });
                }
            }

            // Keep temperature minimal (0.0) so answers stay strictly grounded in the email
            const double minimalTemp = 0.0;
            int tokenBudget = Math.Max(settings.MaxTokens > 0 ? settings.MaxTokens : 350, 1024);

            return await SendStreamingChatCompletionAsync(
                settings,
                messages.ToArray(),
                minimalTemp,
                tokenBudget,
                onVisibleTextUpdated,
                ct);
        }

        private async Task<(bool Success, string ContentOrError)> SendChatCompletionAsync(
            AppSettings settings,
            string systemPrompt,
            string userContent,
            double temperature,
            int maxTokens,
            string cancelledMessage,
            string missingContentMessage,
            CancellationToken ct)
        {
            var messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent }
            };

            var res = await SendChatCompletionCoreAsync(
                settings,
                messages,
                temperature,
                maxTokens,
                stream: false,
                cancelledMessage,
                async (response, token) =>
                {
                    string responseJson = await response.Content.ReadAsStringAsync(token);
                    return ParseNonStreamingResponse(responseJson, missingContentMessage);
                },
                ct);

            return (res.Success, res.Content);
        }

        private async Task<AskMoreStreamResult> SendStreamingChatCompletionAsync(
            AppSettings settings,
            object[] messages,
            double temperature,
            int maxTokens,
            Action<string>? onVisibleTextUpdated,
            CancellationToken ct)
        {
            return await SendChatCompletionCoreAsync(
                settings,
                messages,
                temperature,
                maxTokens,
                stream: true,
                "(Request cancelled)",
                async (response, token) => await ReadStreamingOrJsonResponseAsync(response, onVisibleTextUpdated, token),
                ct);
        }

        private async Task<AskMoreStreamResult> SendChatCompletionCoreAsync(
            AppSettings settings,
            object[] messages,
            double temperature,
            int maxTokens,
            bool stream,
            string cancelledMessage,
            Func<HttpResponseMessage, CancellationToken, Task<AskMoreStreamResult>> handleSuccessResponse,
            CancellationToken ct)
        {
            bool isLlama = string.Equals(settings.AiBackend, "LlamaCpp", StringComparison.OrdinalIgnoreCase);
            string endpointUrl = settings.GetEffectiveEndpointUrl();
            string modelName = settings.GetEffectiveModelName();
            string? apiKey = settings.GetEffectiveApiKey();

            var requestBody = new
            {
                model = modelName,
                messages,
                temperature,
                max_tokens = maxTokens,
                stream
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);

            const int maxRetries = 20; // Up to ~30-40s wait for large models to finish VRAM allocation
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                if (ct.IsCancellationRequested)
                {
                    return new AskMoreStreamResult { Success = false, Content = cancelledMessage, WasStopped = true };
                }

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
                    request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
                    }

                    var completionOpt = stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
                    using var response = await HttpClient.SendAsync(request, completionOpt, ct);

                    if (response.IsSuccessStatusCode)
                    {
                        return await handleSuccessResponse(response, ct);
                    }
                    else
                    {
                        string errorText = await response.Content.ReadAsStringAsync(ct);

                        // If model is still loading into VRAM / RAM (HTTP 503 or "loading model"), pause and retry!
                        bool isModelLoading = (int)response.StatusCode == 503 ||
                                              errorText.Contains("loading model", StringComparison.OrdinalIgnoreCase) ||
                                              errorText.Contains("loading", StringComparison.OrdinalIgnoreCase);

                        if (isModelLoading && attempt < maxRetries - 1)
                        {
                            await Task.Delay(1500, ct);
                            continue;
                        }

                        string cleanError = ParseErrorMessage(errorText, response.StatusCode);
                        string msg = isLlama
                            ? $"llama-server returned an error: {cleanError}"
                            : $"AI service error: {cleanError}";
                        return new AskMoreStreamResult { Success = false, Content = msg };
                    }
                }
                catch (HttpRequestException ex)
                {
                    bool isConnectionRefused = ex.Message.Contains("actively refused", StringComparison.OrdinalIgnoreCase) ||
                                               ex.Message.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase);

                    if (isLlama)
                    {
                        bool isProcessRunning = false;
                        try
                        {
                            isProcessRunning = Process.GetProcessesByName("llama-server").Any(p => !p.HasExited);
                        }
                        catch { }

                        if (!isProcessRunning)
                        {
                            return new AskMoreStreamResult { Success = false, Content = "llama-server is not running or failed to start. Check Live Logs for details." };
                        }

                        if (attempt < maxRetries - 1 && isConnectionRefused)
                        {
                            await Task.Delay(1500, ct);
                            continue;
                        }

                        return new AskMoreStreamResult { Success = false, Content = "llama-server is not responding. Check Live Logs for details." };
                    }
                    else
                    {
                        if (attempt < maxRetries - 1 && isConnectionRefused)
                        {
                            await Task.Delay(1500, ct);
                            continue;
                        }

                        string backendTitle = string.Equals(settings.AiBackend, "Ollama", StringComparison.OrdinalIgnoreCase) ? "Ollama" : "Cloud AI";
                        return new AskMoreStreamResult { Success = false, Content = $"Could not connect to {backendTitle}. Ensure the service is running and accessible." };
                    }
                }
                catch (OperationCanceledException)
                {
                    return new AskMoreStreamResult { Success = false, Content = cancelledMessage, WasStopped = true };
                }
                catch (Exception ex)
                {
                    string msg = isLlama
                        ? $"llama-server error: {ex.Message}"
                        : $"Could not generate response: {ex.Message}";
                    return new AskMoreStreamResult { Success = false, Content = msg };
                }
            }

            string timeoutMsg = isLlama
                ? "llama-server took too long to become ready. Check Live Logs for details."
                : "AI service took too long to respond. Please try again.";
            return new AskMoreStreamResult { Success = false, Content = timeoutMsg };
        }

        private static AskMoreStreamResult ParseNonStreamingResponse(string responseJson, string missingContentMessage)
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var msg))
                {
                    string contentText = msg.TryGetProperty("content", out var cp) && cp.ValueKind == JsonValueKind.String ? cp.GetString() ?? "" : "";
                    string reasoningText = msg.TryGetProperty("reasoning_content", out var rp) && rp.ValueKind == JsonValueKind.String ? rp.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(reasoningText) && msg.TryGetProperty("reasoning", out var rp2) && rp2.ValueKind == JsonValueKind.String)
                    {
                        reasoningText = rp2.GetString() ?? "";
                    }

                    string rawOutput = !string.IsNullOrWhiteSpace(contentText) ? contentText.Trim() : reasoningText.Trim();
                    if (string.IsNullOrWhiteSpace(rawOutput))
                    {
                        return new AskMoreStreamResult { Success = false, Content = "(Empty response from LLM)" };
                    }

                    return new AskMoreStreamResult { Success = true, Content = rawOutput };
                }
            }

            return new AskMoreStreamResult { Success = false, Content = missingContentMessage };
        }

        private static async Task<AskMoreStreamResult> ReadStreamingOrJsonResponseAsync(
            HttpResponseMessage response,
            Action<string>? onVisibleTextUpdated,
            CancellationToken ct)
        {
            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                string responseJson = await response.Content.ReadAsStringAsync(ct);
                var parsed = ParseNonStreamingResponse(responseJson, "(No answer content found in LLM response)");
                if (!parsed.Success) return parsed;

                string cleaned = StripThinkingTags(parsed.Content, keepSummaryMarkerFallback: false);
                if (string.IsNullOrWhiteSpace(cleaned))
                {
                    return new AskMoreStreamResult { Success = false, Content = "(Empty response from LLM)" };
                }

                onVisibleTextUpdated?.Invoke(cleaned);
                return new AskMoreStreamResult { Success = true, Content = cleaned };
            }

            var contentSb = new StringBuilder();
            var reasoningSb = new StringBuilder();
            string lastEmitted = string.Empty;

            using var cancelReg = ct.Register(() =>
            {
                try { response.Dispose(); } catch { }
            });

            try
            {
                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                while (!ct.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync(ct);
                    if (line == null) break;

                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith(":", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        // Handle servers that return plain JSON without application/json Content-Type
                        if (contentSb.Length == 0 && reasoningSb.Length == 0 && line.TrimStart().StartsWith("{", StringComparison.Ordinal))
                        {
                            string fullJson = line + await reader.ReadToEndAsync(ct);
                            var parsed = ParseNonStreamingResponse(fullJson, "(No answer content found in LLM response)");
                            if (!parsed.Success) return parsed;

                            string cleaned = StripThinkingTags(parsed.Content, keepSummaryMarkerFallback: false);
                            if (string.IsNullOrWhiteSpace(cleaned))
                            {
                                return new AskMoreStreamResult { Success = false, Content = "(Empty response from LLM)" };
                            }

                            onVisibleTextUpdated?.Invoke(cleaned);
                            return new AskMoreStreamResult { Success = true, Content = cleaned };
                        }
                        continue;
                    }

                    string data = line.Substring(5).Trim();
                    if (string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    try
                    {
                        using var doc = JsonDocument.Parse(data);
                        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                        {
                            continue;
                        }

                        var firstChoice = choices[0];
                        string deltaContent = string.Empty;
                        string deltaReasoning = string.Empty;

                        if (firstChoice.TryGetProperty("delta", out var delta))
                        {
                            if (delta.TryGetProperty("content", out var cp) && cp.ValueKind == JsonValueKind.String)
                            {
                                deltaContent = cp.GetString() ?? string.Empty;
                            }
                            if (delta.TryGetProperty("reasoning_content", out var rp) && rp.ValueKind == JsonValueKind.String)
                            {
                                deltaReasoning = rp.GetString() ?? string.Empty;
                            }
                            else if (delta.TryGetProperty("reasoning", out var rp2) && rp2.ValueKind == JsonValueKind.String)
                            {
                                deltaReasoning = rp2.GetString() ?? string.Empty;
                            }
                        }
                        else if (firstChoice.TryGetProperty("message", out var msg))
                        {
                            if (msg.TryGetProperty("content", out var cp) && cp.ValueKind == JsonValueKind.String)
                            {
                                deltaContent = cp.GetString() ?? string.Empty;
                            }
                            if (msg.TryGetProperty("reasoning_content", out var rp) && rp.ValueKind == JsonValueKind.String)
                            {
                                deltaReasoning = rp.GetString() ?? string.Empty;
                            }
                        }

                        if (!string.IsNullOrEmpty(deltaReasoning))
                        {
                            reasoningSb.Append(deltaReasoning);
                        }

                        if (!string.IsNullOrEmpty(deltaContent))
                        {
                            contentSb.Append(deltaContent);
                            string visible = ExtractStreamingVisibleText(contentSb.ToString());
                            if (!string.IsNullOrEmpty(visible) && !string.Equals(visible, lastEmitted, StringComparison.Ordinal))
                            {
                                lastEmitted = visible;
                                onVisibleTextUpdated?.Invoke(visible);
                            }
                        }
                    }
                    catch
                    {
                        // Ignore malformed partial SSE frames
                    }
                }
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                string partial = ExtractStreamingVisibleText(contentSb.ToString()).Trim();
                return new AskMoreStreamResult
                {
                    Success = !string.IsNullOrWhiteSpace(partial),
                    Content = partial,
                    WasStopped = true
                };
            }

            if (ct.IsCancellationRequested)
            {
                string partial = ExtractStreamingVisibleText(contentSb.ToString()).Trim();
                return new AskMoreStreamResult
                {
                    Success = !string.IsNullOrWhiteSpace(partial),
                    Content = partial,
                    WasStopped = true
                };
            }

            string rawFinal = contentSb.Length > 0 ? contentSb.ToString() : reasoningSb.ToString();
            string finalClean = StripThinkingTags(rawFinal, keepSummaryMarkerFallback: false);
            if (string.IsNullOrWhiteSpace(finalClean))
            {
                return new AskMoreStreamResult { Success = false, Content = "(Empty response from LLM)" };
            }

            return new AskMoreStreamResult { Success = true, Content = finalClean, WasStopped = false };
        }

        public static string ExtractStreamingVisibleText(string rawContent)
        {
            if (string.IsNullOrEmpty(rawContent)) return string.Empty;

            string text = rawContent;

            // Strip closed thinking/reasoning tags
            text = Regex.Replace(
                text,
                @"<(?:think|thought|reasoning|reflection)>.*?</(?:think|thought|reasoning|reflection)>",
                "",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            text = Regex.Replace(
                text,
                @"\[(?:THINK|THOUGHT|REASONING)\].*?\[\/(?:THINK|THOUGHT|REASONING)\]",
                "",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            text = Regex.Replace(
                text,
                @"^.*?</(?:think|thought|reasoning|reflection)>\s*",
                "",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            string trimmed = text.TrimStart();
            if (string.IsNullOrEmpty(trimmed)) return string.Empty;

            // Suppress unclosed reasoning blocks while streaming
            if (trimmed.StartsWith("<think", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("<thought", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("<reasoning", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("<reflection", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("[THINK", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("[THOUGHT", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("[REASONING", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            // Also suppress partial prefix of an opening tag on the very first token(s)
            if (trimmed.Length < 12)
            {
                string[] openingTags = { "<think>", "<thought>", "<reasoning>", "<reflection>", "[THINK]", "[THOUGHT]", "[REASONING]" };
                foreach (var tag in openingTags)
                {
                    if (tag.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        return string.Empty;
                    }
                }
            }

            return trimmed;
        }

        public static string StripThinkingTags(string rawOutput, bool keepSummaryMarkerFallback = false)
        {
            if (string.IsNullOrWhiteSpace(rawOutput))
                return string.Empty;

            string text = rawOutput.Trim();

            // 1. Remove all closed thinking/reasoning tags: <think>...</think>, <thought>...</thought>, <reasoning>...</reasoning>, <reflection>...</reflection>
            text = Regex.Replace(
                text,
                @"<(?:think|thought|reasoning|reflection)>.*?</(?:think|thought|reasoning|reflection)>",
                "",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            ).Trim();

            // 2. Remove bracketed thinking tags: [THINK]...[/THINK], [THOUGHT]...[/THOUGHT], [REASONING]...[/REASONING]
            text = Regex.Replace(
                text,
                @"\[(?:THINK|THOUGHT|REASONING)\].*?\[\/(?:THINK|THOUGHT|REASONING)\]",
                "",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            ).Trim();

            // 3. Remove orphaned closing tags: </think>, </thought>, </reasoning> if any remain at start
            text = Regex.Replace(
                text,
                @"^.*?</(?:think|thought|reasoning|reflection)>\s*",
                "",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            ).Trim();

            // 4. If text still starts with an unclosed <think> tag, strip opening tag (or scan to Priority/Summary markers if requested)
            if (text.StartsWith("<think>", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("<thought>", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("<reasoning>", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("<reflection>", StringComparison.OrdinalIgnoreCase))
            {
                if (keepSummaryMarkerFallback)
                {
                    var markerMatch = Regex.Match(
                        text,
                        @"(?:Priority|Rank|Summary|Executive Summary)\s*[:=\-]",
                        RegexOptions.IgnoreCase
                    );

                    if (markerMatch.Success)
                    {
                        return text.Substring(markerMatch.Index).Trim();
                    }
                }

                text = Regex.Replace(
                    text,
                    @"^<(?:think|thought|reasoning|reflection)>\s*",
                    "",
                    RegexOptions.IgnoreCase
                ).Trim();
            }

            return text;
        }

        public static (string Summary, int Priority) ParseLlmSummaryAndPriority(string rawOutput)
        {
            if (string.IsNullOrWhiteSpace(rawOutput))
                return ("(Empty response from LLM)", 2);

            string text = StripThinkingTags(rawOutput, keepSummaryMarkerFallback: true);
            if (string.IsNullOrWhiteSpace(text))
                return ("(Empty response from LLM)", 2);

            int priority = 2; // Default to Normal (2)

            // Extract Priority: 1 / Priority: 2 / Priority: 3 / High / Normal / Low
            // Matches formats like:
            // "Priority: 1", "**Priority:** 2", "Priority: 2 (Normal)", "Priority: Low", "1. Priority: 3"
            var priPattern = @"(?i)(?:^|[\r\n\s])(?:\d+\.\s*)?(?:\*\*)?(?:Priority|Rank|Urgency|Level)(?:\*\*)?\s*[:=\-]?\s*(?:\*\*)?\s*\[?\s*(?:Priority\s*)?([1-3]|High|Urgent|Medium|Normal|Low)\b";
            var priMatches = Regex.Matches(text, priPattern);

            if (priMatches.Count > 0)
            {
                var lastPri = priMatches[priMatches.Count - 1];
                string val = lastPri.Groups[1].Value.ToLowerInvariant();
                priority = val switch
                {
                    "1" or "high" or "urgent" => 1,
                    "3" or "low" => 3,
                    _ => 2
                };
            }

            // Extract Summary: ...
            // Handles both "Summary: ... Priority: 2" and "Priority: 2 Summary: ..."
            string cleanSummary = "";
            var sumMarkerPattern = @"(?i)(?:^|[\r\n])(?:\s*(?:\d+\.|\*)\s*)?(?:\*\*)?(?:Summary|Executive Summary)(?:\*\*)?\s*[:=\-]\s*(?:\*\*)?\s*";
            var sumMatches = Regex.Matches(text, sumMarkerPattern);

            for (int i = sumMatches.Count - 1; i >= 0; i--)
            {
                var sm = sumMatches[i];
                string afterMarker = text.Substring(sm.Index + sm.Length).Trim();
                var priInSummary = Regex.Match(afterMarker, @"(?i)[\r\n]+\s*(?:\d+\.\s*)?(?:\*\*)?(?:Priority|Rank|Urgency|Level)(?:\*\*)?\s*[:=\-]");
                string candidate = priInSummary.Success ? afterMarker.Substring(0, priInSummary.Index).Trim() : afterMarker;
                if (Regex.IsMatch(candidate, @"^\[?(?:summary(?:\s+text)?|text|insert summary)\]?$", RegexOptions.IgnoreCase))
                {
                    continue;
                }
                cleanSummary = candidate;
                break;
            }

            // Fallback if no distinct Summary: header found
            if (string.IsNullOrWhiteSpace(cleanSummary))
            {
                cleanSummary = Regex.Replace(text, priPattern + @"[^\r\n]*", "").Trim();
            }

            // Remove any leftover Priority line or placeholder artifact from the summary text
            cleanSummary = Regex.Replace(cleanSummary, @"^(?:\*\*)?(?:Priority|Rank|Urgency|Level)(?:\*\*)?\s*[:=\-]?\s*(?:\*\*)?\s*\[?\s*(?:[1-3]|High|Urgent|Medium|Normal|Low)\b[^\r\n]*", "", RegexOptions.IgnoreCase).Trim();
            cleanSummary = Regex.Replace(cleanSummary, @"^\[?(?:summary(?:\s+text)?|text|insert summary)\]?\s*", "", RegexOptions.IgnoreCase).Trim();
            cleanSummary = cleanSummary.Trim('`', '\r', '\n', ' ', '"', '\'', ':').Trim();

            if (string.IsNullOrWhiteSpace(cleanSummary))
            {
                cleanSummary = "(Summary not available)";
            }

            return (cleanSummary, priority);
        }

        public async Task<(bool Success, string Message)> TestLlmConnectionDetailedAsync(
            string serverUrl,
            string modelName = "default",
            string? apiKey = null,
            CancellationToken ct = default)
        {
            try
            {
                var requestBody = new
                {
                    model = string.IsNullOrWhiteSpace(modelName) ? "default" : modelName,
                    messages = new[]
                    {
                        new { role = "user", content = "Respond with 'OK' if you are online." }
                    },
                    max_tokens = 10
                };

                string jsonPayload = JsonSerializer.Serialize(requestBody);
                using var request = new HttpRequestMessage(HttpMethod.Post, serverUrl);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(15));

                var response = await HttpClient.SendAsync(request, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    return (true, "✓ Connected successfully! Endpoint is active and responding.");
                }

                string errorText = await response.Content.ReadAsStringAsync(cts.Token);
                string cleanMsg = ParseErrorMessage(errorText, response.StatusCode);
                return (false, $"✗ Server returned error {(int)response.StatusCode} ({response.StatusCode}): {cleanMsg}");
            }
            catch (OperationCanceledException)
            {
                return (false, "✗ Connection timed out (15s). Ensure endpoint is accessible.");
            }
            catch (Exception ex)
            {
                return (false, $"✗ Connection failed: {ex.Message}");
            }
        }

        public async Task<bool> TestLlmConnectionAsync(string serverUrl, CancellationToken ct = default)
        {
            var result = await TestLlmConnectionDetailedAsync(serverUrl, "default", null, ct);
            return result.Success;
        }

        private static string ParseErrorMessage(string json, System.Net.HttpStatusCode statusCode)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("error", out var errorProp))
                    {
                        if (errorProp.ValueKind == JsonValueKind.Object && errorProp.TryGetProperty("message", out var msgProp))
                        {
                            return msgProp.GetString() ?? errorProp.ToString();
                        }
                        if (errorProp.ValueKind == JsonValueKind.String)
                        {
                            return errorProp.GetString() ?? errorProp.ToString();
                        }
                        return errorProp.ToString();
                    }
                }
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(json) && json.Length < 160 && !json.Contains("{") && !json.Contains("<html", StringComparison.OrdinalIgnoreCase))
            {
                return json.Trim();
            }

            return statusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "Invalid API key or unauthorized access.",
                System.Net.HttpStatusCode.NotFound => "Endpoint or model not found.",
                System.Net.HttpStatusCode.TooManyRequests => "Rate limit exceeded. Please wait a moment before trying again.",
                System.Net.HttpStatusCode.ServiceUnavailable => "Service is temporarily unavailable.",
                _ => $"Request failed with status {(int)statusCode} ({statusCode})."
            };
        }
    }
}
