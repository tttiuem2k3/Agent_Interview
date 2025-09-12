// File: Services/GeminiLlmClient.cs
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AI_Agent_Basic.Services
{
    /// <summary>
    /// ILlmClient triển khai bằng Google Generative Language API (Gemini).
    /// Model mặc định: "learnlm-2.0-flash-experimental".
    /// </summary>
    public sealed class GeminiLlmClient : ILlmClient, IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _model;

        /// <param name="apiKey">GEMINI_API_KEY</param>
        /// <param name="model">vd: "learnlm-2.0-flash-experimental", "gemini-1.5-flash", "gemini-1.5-pro"</param>
        public GeminiLlmClient(string apiKey, string model = "gemini-2.0-flash", HttpClient? http = null)
        {
            _apiKey = string.IsNullOrWhiteSpace(apiKey) ? throw new ArgumentNullException(nameof(apiKey)) : apiKey;
            _model  = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model;
            _http   = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        /// <summary>
        /// Gọi GenerateContent (non-stream) để đơn giản hóa tích hợp với hệ thống hiện tại.
        /// </summary>
        public async Task<string> CompleteAsync(
            string systemPrompt,
            IEnumerable<(string role, string content)> history,
            string userMessage,
            CancellationToken ct)
        {
            // Map history -> Gemini contents
            var contents = new List<object>();
            foreach (var (role, content) in history)
            {
                if (string.IsNullOrWhiteSpace(content)) continue;
                var mappedRole = role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "model" : "user";
                contents.Add(new
                {
                    role = mappedRole,
                    parts = new[] { new { text = content } }
                });
            }
            // Current user turn = "INSERT_INPUT_HERE" tương đương Python mẫu
            contents.Add(new
            {
                role = "user",
                parts = new[] { new { text = userMessage } }
            });

            var requestBody = new
            {
                // Python: generate_content_config = types.GenerateContentConfig()
                // => để trống / dùng default. Ở đây có thể set temperature nếu muốn.
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents,
                generationConfig = new
                {
                    temperature = 0.3
                }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

            using var resp = await _http.PostAsJsonAsync(url, requestBody, ct);
            var respText = await resp.Content.ReadAsStringAsync(ct);

            if (resp.IsSuccessStatusCode)
            {
                // Lấy text trong candidates[0].content.parts[*].text
                try
                {
                    using var doc = JsonDocument.Parse(respText);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("candidates", out var cands) && cands.GetArrayLength() > 0)
                    {
                        var parts = cands[0].GetProperty("content").GetProperty("parts");
                        foreach (var p in parts.EnumerateArray())
                        {
                            if (p.TryGetProperty("text", out var t) && !string.IsNullOrWhiteSpace(t.GetString()))
                                return t.GetString()!;
                        }
                    }
                }
                catch { /* ignore and fallthrough */ }
                return string.Empty;
            }

            // Parse lỗi để nhận biết quota/rate-limit
            var (status, code) = TryParseError(respText);

            // RESOURCE_EXHAUSTED hoặc 429 => coi như quota/rate-limit
            if (resp.StatusCode == (HttpStatusCode)429 || string.Equals(status, "RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase))
                throw new InsufficientQuotaException(respText);

            // 400/401/403/404 => lỗi cấu hình/permission/model => ném ra thẳng
            if (resp.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                throw new HttpRequestException($"Gemini error {(int)resp.StatusCode}: {respText}");

            // Khác => ném chi tiết
            throw new HttpRequestException($"Gemini unexpected {(int)resp.StatusCode}: {respText}");
        }

        public void Dispose() => _http.Dispose();

        private static (string? status, string? code) TryParseError(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var err = doc.RootElement.GetProperty("error");
                string? status = err.TryGetProperty("status", out var s) ? s.GetString() : null;
                string? code   = err.TryGetProperty("code", out var c)   ? c.GetRawText() : null;
                return (status, code);
            }
            catch { return (null, null); }
        }
    }
}
