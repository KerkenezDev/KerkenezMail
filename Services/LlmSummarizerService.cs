using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EmailSummarizer.Models;

namespace EmailSummarizer.Services
{
    public class LlmSummarizerService
    {
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        public async Task<string> SummarizeEmailAsync(
            EmailItem email,
            AppSettings settings,
            CancellationToken ct = default)
        {
            var userContent = $"Subject: {email.Subject}\r\nFrom: {email.Sender}\r\nContent:\r\n{email.CleanBody}";
            string endpointUrl = settings.GetEffectiveEndpointUrl();
            string modelName = settings.GetEffectiveModelName();
            string? apiKey = settings.GetEffectiveApiKey();

            var requestBody = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "system", content = settings.SystemPrompt },
                    new { role = "user", content = userContent }
                },
                temperature = settings.Temperature,
                max_tokens = settings.MaxTokens > 0 ? settings.MaxTokens : 350,
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
                        if (firstChoice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var contentProp))
                        {
                            string summary = contentProp.GetString()?.Trim() ?? "";
                            return string.IsNullOrWhiteSpace(summary) ? "(Empty response from LLM)" : summary;
                        }
                    }

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
