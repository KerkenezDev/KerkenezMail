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

        public async Task<string> SummarizeEmailAsync(
            EmailItem email,
            AppSettings settings,
            CancellationToken ct = default)
        {
            var userContent = $"Analyze the following email, assign a Priority rank (1 = High/Urgent, 2 = Normal/Medium, 3 = Low/Newsletter), and provide a concise 1-3 sentence executive summary.\r\n\r\n" +
                              $"Subject: {email.Subject}\r\n" +
                              $"From: {email.Sender}\r\n" +
                              $"Email Content:\r\n{email.CleanBody}\r\n\r\n" +
                              $"Output strictly in this format:\r\n" +
                              $"Priority: [1/2/3]\r\n" +
                              $"Summary: [summary text]";

            string endpointUrl = settings.GetEffectiveEndpointUrl();
            string modelName = settings.GetEffectiveModelName();
            string? apiKey = settings.GetEffectiveApiKey();

            string systemPrompt = settings.SystemPrompt;
            if (!systemPrompt.Contains("Priority", StringComparison.OrdinalIgnoreCase))
            {
                systemPrompt += "\r\n\r\nRequired Output format:\r\nPriority: [1/2/3]\r\nSummary: [summary text]";
            }

            // Allocate a generous token limit (minimum 2048) to seamlessly accommodate thinking/reasoning models (e.g. DeepSeek-R1, QwQ)
            int tokenBudget = Math.Max(settings.MaxTokens > 0 ? settings.MaxTokens : 350, 2048);

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
                    string cleanError = ParseErrorMessage(errorText, response.StatusCode);
                    return $"(LLM Error {response.StatusCode}: {cleanError})";
                }
            }
            catch (HttpRequestException ex)
            {
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

            // Extract Priority: 1 / Priority: 2 / Priority: 3 (supports markdown bolding like **Priority:** 1, brackets [1], etc.)
            var priMatch = Regex.Match(
                text,
                @"(?:^|[\r\n\s])(?:\*\*)?(?:Priority|Rank|Urgency|Level)(?:\*\*)?\s*[:=\-]?\s*\[?\s*([1-3])\s*\]?",
                RegexOptions.IgnoreCase
            );

            if (priMatch.Success && int.TryParse(priMatch.Groups[1].Value, out int parsedPri))
            {
                priority = Math.Clamp(parsedPri, 1, 3);
            }

            string cleanSummary = text;

            // Extract Summary: ...
            var sumMatch = Regex.Match(
                text,
                @"(?:^|[\r\n\s])(?:\*\*)?(?:Summary|Executive Summary)(?:\*\*)?\s*[:=\-]\s*(.*)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            );

            if (sumMatch.Success && !string.IsNullOrWhiteSpace(sumMatch.Groups[1].Value))
            {
                cleanSummary = sumMatch.Groups[1].Value.Trim();
            }
            else if (priMatch.Success)
            {
                // Remove the priority line from summary
                cleanSummary = Regex.Replace(
                    text,
                    @"^(?:\*\*)?(?:Priority|Rank|Urgency|Level)(?:\*\*)?\s*[:=\-]?\s*\[?\s*[1-3]\s*\]?\s*[\r\n]+",
                    "",
                    RegexOptions.IgnoreCase
                ).Trim();
            }

            // Remove any trailing markdown artifacts or quotes
            cleanSummary = cleanSummary.Trim('`', '\r', '\n', ' ');

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
