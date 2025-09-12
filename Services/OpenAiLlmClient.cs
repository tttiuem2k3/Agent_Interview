// File: Services/OpenAiLlmClient.cs
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AI_Agent_Basic.Services
{
    public sealed class OpenAiLlmClient : ILlmClient, IDisposable
    {
        private readonly HttpClient _Http;
        private readonly string _ApiKey;
        private readonly string _Model;

        // 👇 Default model 
        public OpenAiLlmClient(string apiKey, string model = "gpt-4o-mini", HttpClient? http = null)
        {
            _ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _Model  = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model;
            _Http   = http ?? new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            _Http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _ApiKey);
        }

        public async Task<string> CompleteAsync(
            string systemPrompt,
            IEnumerable<(string role, string content)> history,
            string userMessage,
            CancellationToken ct)
        {
            var msgs = new List<object> { new { role = "system", content = systemPrompt } };
            foreach (var (role, content) in history) msgs.Add(new { role, content });
            msgs.Add(new { role = "user", content = userMessage });

            var req = new
            {
                model = _Model,         // ← sẽ là "gpt-5" nếu bạn không truyền khác
                messages = msgs,
                temperature = 0.3
            };

            const int maxRetries = 5;
            var attempt = 0;

            while (true)
            {
                attempt++;
                using var resp = await _Http.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", req, ct);

                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
                    return json?.choices?[0]?.message?.content ?? string.Empty;
                }

                // Key sai/quyền truy cập model chưa được bật → dừng sớm và báo lỗi rõ ràng
                if (resp.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                {
                    var errText = await resp.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException($"OpenAI error {(int)resp.StatusCode}: {errText}");
                }

                // 429 hoặc 5xx → retry với backoff
                if (resp.StatusCode == (HttpStatusCode)429 || (int)resp.StatusCode >= 500)
                {
                    if (attempt >= maxRetries)
                    {
                        var finalText = await resp.Content.ReadAsStringAsync(ct);
                        throw new HttpRequestException($"OpenAI rate-limited/5xx after {attempt} attempts. Last response: {finalText}");
                    }

                    TimeSpan delay = TimeSpan.FromSeconds(2 * Math.Pow(2, attempt - 1)); // 2,4,8,16,32s
                    if (resp.Headers.TryGetValues("Retry-After", out var values))
                    {
                        if (int.TryParse(System.Linq.Enumerable.First(values), out int sec) && sec > 0)
                            delay = TimeSpan.FromSeconds(sec);
                    }

                    await Task.Delay(delay, ct);
                    continue;
                }

                var text = await resp.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"OpenAI unexpected status {(int)resp.StatusCode}: {text}");
            }
        }

        public void Dispose() => _Http.Dispose();

        private sealed class ChatResponse { public List<Choice>? choices { get; set; } }
        private sealed class Choice { public ChatMessage? message { get; set; } }
        private sealed class ChatMessage { public string? role { get; set; } public string? content { get; set; } }
    }
}
