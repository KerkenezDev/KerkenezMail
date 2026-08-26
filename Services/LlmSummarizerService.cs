using System;
using System.Net.Http;
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

            var requestBody = new
            {
                messages = new[]
                {
                    new { role = "system", content = settings.SystemPrompt },
                    new { role = "user", content = userContent }
                },
                temperature = 0.0,
                max_tokens = 180,
                stream = false
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await HttpClient.PostAsync(settings.LlamaServerUrl, content, ct);
                
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
                    return $"(LLM Server Error {response.StatusCode}: {errorText})";
                }
            }
            catch (HttpRequestException)
            {
                return $"(Could not connect to LLM server at {settings.LlamaServerUrl}. Ensure llama-server is running.)";
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

        public async Task<bool> TestLlmConnectionAsync(string serverUrl, CancellationToken ct = default)
        {
            try
            {
                // Simple completion test
                var requestBody = new
                {
                    messages = new[]
                    {
                        new { role = "user", content = "Respond with 'OK' if you are online." }
                    },
                    max_tokens = 10
                };

                string jsonPayload = JsonSerializer.Serialize(requestBody);
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await HttpClient.PostAsync(serverUrl, content, ct);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
