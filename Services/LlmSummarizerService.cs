using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EmailSummarizer.Models;

namespace EmailSummarizer.Services
{
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
                return "Context Note: Mass mailing list / newsletter format detected.\r\n";
            }
            else if (email.IsMailingList || isThreadOrReply)
            {
                return "Context Note: Automated notification or discussion thread.\r\n";
            }

            return string.Empty;
        }

        public async Task<string> SummarizeEmailAsync(
            EmailItem email,
            AppSettings settings,
            CancellationToken ct = default)
        {
            string emailContentForLlm = PrepareEmailBodyForSummary(email.CleanBody, settings.MaxSummaryEmailChars);
            string metadataNotice = GetEmailContextNotice(email);

            var userContent = $"Subject: {email.Subject}\r\n" +
                              $"From: {email.Sender}\r\n" +
                              metadataNotice +
                              $"Email Content:\r\n{emailContentForLlm}\r\n\r\n" +
                              $"Read the email above and provide the Priority and Summary.\r\n" +
                              $"Do NOT output scratchpad notes, drafts, or numbered analysis steps.\r\n" +
                              $"Output strictly in this format:\r\n" +
                              $"Priority: <1, 2, or 3>\r\n" +
                              $"Summary: <1-3 sentence brief>";

            string endpointUrl = settings.GetEffectiveEndpointUrl();
            string modelName = settings.GetEffectiveModelName();
            string? apiKey = settings.GetEffectiveApiKey();

            string systemPrompt = settings.SystemPrompt;
            if (!systemPrompt.Contains("Priority", StringComparison.OrdinalIgnoreCase))
            {
                systemPrompt += "\r\n\r\nRequired Output format (do not include scratchpad notes):\r\nPriority: <1, 2, or 3>\r\nSummary: <1-3 sentence brief>";
            }
            if (!systemPrompt.Contains("validation failure", StringComparison.OrdinalIgnoreCase) && !systemPrompt.Contains("signals", StringComparison.OrdinalIgnoreCase))
            {
                systemPrompt += "\r\n* Note: Errors, validation failures, and action requests are Priority 1. Marketing promos and bulk digests are Priority 3 (Low).";
            }

            // Strict user preference: respect configured token limit without forced overrides
            int tokenBudget = settings.MaxTokens > 0 ? settings.MaxTokens : 350;

            var requestBody = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userContent }
                },
                temperature = settings.Temperature,
                max_tokens = tokenBudget,
                stream = false
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);

            const int maxRetries = 20; // Up to ~30-40s wait for large models to finish VRAM allocation
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                if (ct.IsCancellationRequested) return "(Summarization cancelled)";

                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
                    request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
                    }

                    var response = await HttpClient.SendAsync(request, ct);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string responseJson = await response.Content.ReadAsStringAsync(ct);
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
                                    email.Priority = 2;
                                    return "(Empty response from LLM)";
                                }

                                var (cleanSummary, parsedPriority) = ParseLlmSummaryAndPriority(rawOutput);
                                email.Priority = parsedPriority;
                                return cleanSummary;
                            }
                        }

                        email.Priority = 2;
                        return "(No summary content found in LLM response)";
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
                        return $"(LLM Error {response.StatusCode}: {cleanError})";
                    }
                }
                catch (HttpRequestException ex)
                {
                    // If connection refused during initial server launch warmup, wait and retry
                    if (attempt < maxRetries - 1 && (ex.Message.Contains("actively refused") || ex.Message.Contains("No connection could be made")))
                    {
                        await Task.Delay(1500, ct);
                        continue;
                    }

                    return $"(Could not reach LLM endpoint at {endpointUrl}. Ensure the AI service is running or check your network/API key: {ex.Message})";
                }
                catch (OperationCanceledException)
                {
                    return "(Summarization cancelled)";
                }
                catch (Exception ex)
                {
                    return $"(Error generating summary: {ex.Message})";
                }
            }

            return "(Failed to get summary: Model did not become ready in time)";
        }

        public static (string Summary, int Priority) ParseLlmSummaryAndPriority(string rawOutput)
        {
            if (string.IsNullOrWhiteSpace(rawOutput))
                return ("(Empty response from LLM)", 2);

            string text = rawOutput.Trim();

            // 1. Remove all closed thinking/reasoning tags: <think>...</think>, <thought>...</thought>, <reasoning>...</reasoning>, <reflection>...</reflection>
            text = Regex.Replace(
                text,
                @"<(?:think|thought|reasoning|reflection)>.*?</(?:think|thought|reasoning|reflection)>",
                "",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            ).Trim();

            // 2. Remove bracketed thinking tags: [THINK]...[/THINK], [THOUGHT]...[/THOUGHT]
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

            // 4. If text still starts with an unclosed <think> tag, strip up to </think> or up to Priority/Summary keywords
            if (text.StartsWith("<think>", StringComparison.OrdinalIgnoreCase) || 
                text.StartsWith("<thought>", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("<reasoning>", StringComparison.OrdinalIgnoreCase))
            {
                var markerMatch = Regex.Match(
                    text,
                    @"(?:Priority|Rank|Summary|Executive Summary)\s*[:=\-]",
                    RegexOptions.IgnoreCase
                );

                if (markerMatch.Success)
                {
                    text = text.Substring(markerMatch.Index).Trim();
                }
                else
                {
                    text = Regex.Replace(
                        text,
                        @"^<(?:think|thought|reasoning|reflection)>\s*",
                        "",
                        RegexOptions.IgnoreCase
                    ).Trim();
                }
            }

            int priority = 2; // Default to Normal (2)

            // Extract Priority: 1 / Priority: 2 / Priority: 3 (take the LAST match in case model drafted in scratchpad)
            var priMatches = Regex.Matches(
                text,
                @"(?:^|[\r\n\s])(?:\*\*)?(?:Priority|Rank|Urgency|Level)(?:\*\*)?\s*[:=\-]?\s*\[?\s*([1-3])\s*\]?",
                RegexOptions.IgnoreCase
            );

            if (priMatches.Count > 0)
            {
                var lastPri = priMatches[priMatches.Count - 1];
                if (int.TryParse(lastPri.Groups[1].Value, out int parsedPri))
                {
                    priority = Math.Clamp(parsedPri, 1, 3);
                }
            }

            // Extract Summary: ...
            // In thinking models, the model may write scratchpad steps or repeat a placeholder "Summary: [summary text]" before its actual summary.
            // We search for all "Summary:" headers and take the LAST meaningful non-placeholder match.
            string cleanSummary = "";
            var sumMatches = Regex.Matches(
                text,
                @"(?:^|[\r\n])(?:\s*(?:\d+\.|\*)\s*)?(?:\*\*)?(?:Summary|Executive Summary)(?:\*\*)?\s*[:=\-]\s*(.+)",
                RegexOptions.IgnoreCase
            );

            for (int i = sumMatches.Count - 1; i >= 0; i--)
            {
                string candidate = sumMatches[i].Groups[1].Value.Trim();
                // Skip placeholder like [summary text], <summary text>, [text]
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
                if (priMatches.Count > 0)
                {
                    var lastPri = priMatches[priMatches.Count - 1];
                    int idx = lastPri.Index + lastPri.Length;
                    if (idx < text.Length)
                    {
                        cleanSummary = text.Substring(idx).Trim();
                    }
                }
                else
                {
                    cleanSummary = text;
                }
            }

            // Remove any leftover Priority line or placeholder artifact from the summary text
            cleanSummary = Regex.Replace(cleanSummary, @"^(?:\*\*)?(?:Priority|Rank|Urgency|Level)(?:\*\*)?\s*[:=\-]?\s*\[?\s*[1-3]\s*\]?\s*", "", RegexOptions.IgnoreCase).Trim();
            cleanSummary = Regex.Replace(cleanSummary, @"^\[?(?:summary(?:\s+text)?|text)\]?\s*", "", RegexOptions.IgnoreCase).Trim();
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
                        return errorProp.ToString();
                    }
                }
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(json) && json.Length < 200)
            {
                return json.Trim();
            }

            return statusCode.ToString();
        }
    }
}
