using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AI_Agent_Basic.Domain;

namespace AI_Agent_Basic.Services
{
    public sealed class ScoringService
    {
        private readonly ILlmClient? _llm;
        private const double AlphaKeyword = 0.30; // 30% keyword, 70% semantic

        public ScoringService(ILlmClient? llm = null) => _llm = llm;

        // --------- PUBLIC API ---------

        public (double Score, string Comment) ScoreAnswer(string answer, InterviewQuestion q)
        {
            var (coverage, kwComment) = ComputeKeywordCoverage(answer, q);
            // Nếu muốn giữ min weight = 1 thì dùng Math.Max(1, q.Weight); nếu không thì dùng q.Weight
            double score = Math.Round(coverage * q.Weight, 2);
            return (score, kwComment);
        }

        public async Task<(double Score, string Comment)> ScoreAnswerSmartAsync(
            string answer,
            InterviewQuestion q,
            CancellationToken ct)
        {
            var (kwCoverage, kwComment) = ComputeKeywordCoverage(answer, q);

            if (_llm is null)
            {
                double scoreKo = Math.Round(kwCoverage * q.Weight, 2);
                return (scoreKo, $"{kwComment} (keyword-only)");
            }

            try
            {
                var sys = "Bạn là giám khảo kỹ thuật. Chấm mức độ đúng ngữ nghĩa so với đáp án mẫu. Trả về JSON duy nhất.";

                var user = $@"
                    Câu hỏi: {q.Text}
                    Trọng số tối đa: {q.Weight}
                    Đáp án mẫu (model_answer): {q.ModelAnswer}
                    Keywords gợi ý: {q.KeywordsCsv}
                    Câu trả lời ứng viên: {answer}

                    Yêu cầu:
                    - semantic_score: số thực 0..1 (1 = rất sát, 0 = chệch hẳn).
                    - reasoning: 1-2 câu giải thích ngắn.
                    Trả về đúng JSON:
                    {{""semantic_score"": number, ""reasoning"": ""string""}}
";

                var raw = await _llm.CompleteAsync(sys, Array.Empty<(string role, string content)>(), user, ct);
                var json = ExtractJsonObject(raw) ?? raw; // cố gắng bóc JSON nếu kèm text

                var parsed = ParseSemantic(json);
                if (parsed is null)
                {
                    // thử lần nữa: gỡ code fences ```json ... ```
                    var cleaned = StripCodeFences(raw);
                    json = ExtractJsonObject(cleaned) ?? cleaned;
                    parsed = ParseSemantic(json);
                }

                if (parsed is null)
                {
                    double scoreKo = Math.Round(kwCoverage * q.Weight, 2);
                    return (scoreKo, $"{kwComment}");
                }

                var sem = Clamp01(parsed.SemanticScore);
                double combined = q.Weight * (AlphaKeyword * kwCoverage + (1 - AlphaKeyword) * sem);
                double final = Math.Round(Clamp(combined, 0, q.Weight), 2);

                string comment = $"LLM: {parsed.Reasoning?.Trim()} | Keyword: {kwComment} | α={AlphaKeyword:0.##}";
                return (final, comment);
            }
            catch (InsufficientQuotaException)
            {
                double scoreKo = Math.Round(kwCoverage * q.Weight, 2);
                return (scoreKo, $"{kwComment} (fallback keyword: insufficient_quota)");
            }
            catch
            {
                double scoreKo = Math.Round(kwCoverage * q.Weight, 2);
                return (scoreKo, $"{kwComment} (fallback keyword: error)");
            }
        }

        // --------- HELPERS ---------

        private sealed class SemanticDto
        {
            [JsonPropertyName("semantic_score")] public double SemanticScore { get; set; }
            [JsonPropertyName("reasoning")] public string? Reasoning { get; set; }
        }

        private static SemanticDto? ParseSemantic(string json)
        {
            try
            {
                var dto = JsonSerializer.Deserialize<SemanticDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (dto is null) return null;
                dto.SemanticScore = Clamp01(dto.SemanticScore);
                return dto;
            }
            catch { return null; }
        }

        // Bóc object JSON đầu tiên từ chuỗi có thể kèm text
        private static string? ExtractJsonObject(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            int start = s.IndexOf('{');
            if (start < 0) return null;
            int depth = 0;
            for (int i = start; i < s.Length; i++)
            {
                if (s[i] == '{') depth++;
                else if (s[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return s.Substring(start, i - start + 1);
                    }
                }
            }
            return null;
        }

        private static string StripCodeFences(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // loại ```json ... ``` hoặc ``` ...
            var cleaned = s.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                           .Replace("```", "");
            return cleaned.Trim();
        }

        // Keyword coverage — robust: bỏ dấu + lowercase
        private static (double Coverage, string Comment) ComputeKeywordCoverage(string answer, InterviewQuestion q)
        {
            // 0) Input rỗng
            if (string.IsNullOrWhiteSpace(answer))
                return (0.0, "Trả lời trống.");

            // 1) Chuẩn hóa answer: bỏ dấu + lowercase
            string ansNorm = RemoveDiacritics(answer).ToLowerInvariant();

            // Token hóa: chỉ giữ chuỗi chữ cái (hỗ trợ tiếng Việt đã bỏ dấu)
            // => ví dụ "indexing," -> token "indexing"
            var ansTokens = System.Text.RegularExpressions.Regex
                .Matches(ansNorm, @"\p{L}+")
                .Select(m => m.Value)
                .ToHashSet();

            // Đồng thời giữ phiên bản câu trả lời đã "gọn" để match cụm từ nhiều từ
            // (Một dấu cách giữa các từ)
            string ansFlat = System.Text.RegularExpressions.Regex.Replace(ansNorm, @"\s+", " ").Trim();

            // 2) Parse & chuẩn hóa danh sách keywords
            var rawKeywords = (q.KeywordsCsv ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Bỏ dấu & lowercase & loại trùng (theo canonical)
            var keywords = rawKeywords
                .Select(k => RemoveDiacritics(k).ToLowerInvariant())
                .Distinct()
                .ToArray();

            if (keywords.Length == 0)
                return (0.0, "Không có tiêu chí keywords để tham chiếu.");

            // 3) Hàm khớp 1 từ khóa
            bool HitKeyword(string kw)
            {
                // Nhiều từ (cụm): dùng regex boundary cho cụm
                if (kw.Contains(' '))
                {
                    // \bkw\b nhưng cho tiếng có dấu đã bỏ => dùng lookaround bằng non-letter
                    // Đảm bảo khoảng trắng đơn trong kw
                    string kwFlat = System.Text.RegularExpressions.Regex.Replace(kw, @"\s+", " ").Trim();
                    var pat = $@"(?<!\p{{L}}){System.Text.RegularExpressions.Regex.Escape(kwFlat)}(?!\p{{L}})";
                    return System.Text.RegularExpressions.Regex.IsMatch(ansFlat, pat);
                }

                // 1 từ: kiểm tra token bằng:
                // - trùng khớp tuyệt đối với một token
                if (ansTokens.Contains(kw)) return true;

                // - hoặc khớp "stem" đơn giản: kw = index -> index|indexes|indexed|indexing|indexer|indexers
                //   (áp cho từ khóa tiếng Anh phổ biến)
                foreach (var t in ansTokens)
                {
                    if (t == kw) return true;
                    if (t.StartsWith(kw) && (t.Length - kw.Length) <= 3) // ví dụ index + ing/ed/es/er (<=3)
                        return true;
                }
                return false;
            }

            int hit = keywords.Count(HitKeyword);
            double coverage = (double)hit / keywords.Length;

            string comment = hit == 0 ? "Chưa chạm ý chính."
                            : coverage < 0.5 ? "Cần chi tiết hơn."
                            : "Tốt, bao phủ phần lớn ý.";

            return (Clamp01(coverage), comment);
        }

        // Bỏ dấu unicode (cho tiếng Việt)
        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(capacity: normalized.Length);
            foreach (var c in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);
    }
}
