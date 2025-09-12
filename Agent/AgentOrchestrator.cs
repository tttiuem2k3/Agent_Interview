using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AI_Agent_Basic.Domain;
using AI_Agent_Basic.Infrastructure;
using AI_Agent_Basic.Services;
using AI_Agent_Basic.Tools;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Globalization;
using System.Text;

namespace AI_Agent_Basic.Agent
{
    public sealed class AgentOrchestrator
    {
        private readonly ILlmClient _Llm;
        private readonly AgentDbContext _Db;
        private readonly QuestionBankTool _QTool;
        private readonly ScoringService _Scoring;
        private readonly LeaderMatchingService _Matcher;
        private readonly SchedulingService _Scheduler;
        private readonly EmailService _Email;

        private readonly List<(string role, string content)> _History = new();

        private const int ThrottleDelayMs = 500;

        private const string SysPrompt = @"
Bạn là **AI Agent phỏng vấn tự động của công ty Asoft**. Hãy giao tiếp tự nhiên, ấm áp, chuyên nghiệp; cá nhân hoá ngôn ngữ theo bối cảnh.
Quy trình:
1) Giới thiệu ngắn về bản thân (AI Agent phỏng vấn tự động của Asoft), chào hỏi để tạo không khí thoải mái và chuyên nghiệp.
2) Hỏi người dùng các câu hỏi để khai thác thông tin: họ tên, email, số điện thoại (gộp một câu).
3) Sau đó trình bày và hỏi người dùng muốn ứng tuyển vị trí nào, level nào (1/2/3 hoặc fresher/junior/senior). Luôn kiểm tra thông tin đã đủ chưa; khi đủ mới chuyển bước tiếp theo. Hỏi súc tích; nếu lạc đề thì nhắc khéo.
4) Khi đã đủ thông tin, trao đổi thân thiện về mô tả vị trí: dùng dữ liệu mô tả (description) và required_skills_csv từ hệ thống, diễn giải dễ hiểu, khích lệ.
5) Phỏng vấn: lấy câu hỏi theo Position + Level (tool nội bộ) và hỏi lần lượt. Sau mỗi câu trả lời, đưa nhận xét ngắn gọn.
6) Kết thúc: tính tổng điểm. Pass nếu >=60 → thông báo sẽ đặt lịch vòng 2 với leader. Fail nếu <60 → phản hồi tế nhị, mang tính xây dựng.
7) Luôn giữ thái độ chuyên nghiệp, thân thiện, tích cực; chào tạm biệt lịch sự khi kết thúc.
Ngôn ngữ: tiếng Việt; nếu ứng viên dùng tiếng Anh thì chuyển sang tiếng Anh.";

        public AgentOrchestrator(
            ILlmClient llm,
            AgentDbContext db,
            QuestionBankTool qTool,
            ScoringService scoring,
            LeaderMatchingService matcher,
            SchedulingService scheduler,
            EmailService email)
        {
            _Llm = llm; _Db = db; _QTool = qTool; _Scoring = scoring;
            _Matcher = matcher; _Scheduler = scheduler; _Email = email;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== AI Interview Agent (LLM) — Asoft ===");

            // (1) GIỚI THIỆU & CHÀO HỎI
            var intro = await AskModelAsync(
                "Hãy giới thiệu ngắn gọn về bản thân là AI Agent phỏng vấn của Asoft, chào hỏi thân thiện và mời ứng viên bắt đầu.",
                ct);
            Console.WriteLine(intro);
            _History.Add(("assistant", intro));

            // (2) KHAI THÁC THÔNG TIN 
            string name = "", email = "", phone = "";

            var intakeAsk = await AskModelAsync(
                "Hãy đặt một câu hỏi duy nhất (ngắn gọn, thân thiện) yêu cầu ứng viên cung cấp HỌ TÊN, EMAIL, SỐ ĐIỆN THOẠI trong cùng 1 câu trả lời.",
                ct);
            Console.WriteLine(intakeAsk);
            _History.Add(("assistant", intakeAsk));

            Console.Write("> ");
            var intakeReply = (Console.ReadLine() ?? "").Trim();
            _History.Add(("user", intakeReply));

            // LLM chuẩn hoá + regex fallback
            var (n1, e1, p1) = await NormalizeContactByLlmAsync(intakeReply, ct);
            if (!string.IsNullOrWhiteSpace(n1)) name  = n1!;
            if (!string.IsNullOrWhiteSpace(e1)) email = e1!;
            if (!string.IsNullOrWhiteSpace(p1)) phone = p1!;
            UpdateContactInfo(intakeReply, ref name, ref email, ref phone); // fallback bổ sung

            int retry = 0;
            while ((string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phone)) && retry < 5)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    Console.WriteLine("Bạn vui lòng cho biết HỌ TÊN đầy đủ của bạn là gì?");
                    Console.Write("> ");
                    var reply = (Console.ReadLine() ?? "").Trim();
                    _History.Add(("user", reply));
                    var (nn, _, _) = await NormalizeContactByLlmAsync(reply, ct);
                    if (!string.IsNullOrWhiteSpace(nn)) name = nn!;
                    UpdateContactInfo(reply, ref name, ref email, ref phone);
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    Console.WriteLine("Bạn có thể cho mình xin EMAIL hợp lệ được không? (ví dụ: example@domain.com)");
                    Console.Write("> ");
                    var reply = (Console.ReadLine() ?? "").Trim();
                    _History.Add(("user", reply));
                    var (_, ne, _) = await NormalizeContactByLlmAsync(reply, ct);
                    if (!string.IsNullOrWhiteSpace(ne)) email = ne!;
                    UpdateContactInfo(reply, ref name, ref email, ref phone);
                }

                if (string.IsNullOrWhiteSpace(phone))
                {
                    Console.WriteLine("Bạn vui lòng cung cấp SỐ ĐIỆN THOẠI liên hệ (ví dụ: 090xxxxxxx hoặc +8490xxxxxxx).");
                    Console.Write("> ");
                    var reply = (Console.ReadLine() ?? "").Trim();
                    _History.Add(("user", reply));
                    var (_, _, np) = await NormalizeContactByLlmAsync(reply, ct);
                    if (!string.IsNullOrWhiteSpace(np)) phone = np!;
                    UpdateContactInfo(reply, ref name, ref email, ref phone);
                }

                retry++;
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phone))
            {
                Console.WriteLine("Rất tiếc, bạn chưa cung cấp đủ thông tin bắt buộc. Hẹn gặp lại!");
                return;
            }

            // Danh sách vị trí đang tuyển (positions_interview)
            var activePositions = await GetActivePositionsAsync(ct);
            if (activePositions.Count == 0)
            {
                Console.WriteLine("Hiện tại hệ thống chưa có vị trí tuyển dụng. Hẹn gặp lại!");
                return;
            }

            Console.WriteLine("\nHiện tại, Asoft đang tuyển dụng các vị trí sau:");
            foreach (var p in activePositions) Console.WriteLine($"- {p}");
            Console.WriteLine();

            // Hỏi vị trí: LLM hiểu mềm + fuzzy local (1 lần hỏi + 1 lần hỏi lại)
            var posName = await AskPositionWithVerificationAsync(activePositions, ct);
            if (string.IsNullOrWhiteSpace(posName))
            {
                Console.WriteLine("Rất tiếc, bạn chưa cung cấp VỊ TRÍ phù hợp. Hẹn gặp lại!");
                return;
            }

            // Hỏi level: LLM hiểu mềm (1 lần hỏi + 1 lần hỏi lại)
            var levelText = await AskLevelWithVerificationAsync(ct);
            if (string.IsNullOrWhiteSpace(levelText))
            {
                Console.WriteLine("Rất tiếc, bạn chưa cung cấp LEVEL hợp lệ. Hẹn gặp lại!");
                return;
            }
            int level = int.Parse(levelText);

            // Lưu ứng viên
            var candidate = await _Db.Candidates
                .FirstOrDefaultAsync(c => c.Email == email, ct);

            if (candidate == null)
            {
                candidate = new Candidate
                {
                    FullName = name,
                    Email    = email,
                    Phone    = phone           
                };
                await _Db.Candidates.AddAsync(candidate, ct);
            }
            else
            {
                // cập nhật nếu trống
                if (string.IsNullOrWhiteSpace(candidate.FullName)) candidate.FullName = name;
                if (string.IsNullOrWhiteSpace(candidate.Phone))    candidate.Phone    = phone;
            }
            await _Db.SaveChangesAsync(ct);

            // Tìm Position entity theo tên chuẩn hoá (case-insensitive + fuzzy fallback)
            var position = await FindPositionEntityAsync(posName, ct);
            if (position == null)
            {
                Console.WriteLine($"Hiện tại hệ thống không tìm thấy vị trí phù hợp với '{posName}'. Hẹn gặp lại bạn!");
                return;
            }

            // (3) TRAO ĐỔI MÔ TẢ VỊ TRÍ
            string desc = position.Description?.Trim() ?? "";
            string skills = position.RequiredSkillsCsv?.Trim() ?? "";
            var roleIntro = await AskModelAsync(
                $"Dựa vào mô tả về công việc sau, hãy giải thích thân thiện (3-5 câu), nhấn mạnh yêu cầu chính và kỹ năng quan trọng.\n" +
                $"[MÔ TẢ]: {desc}\n[SKILLS]: {skills}",
                ct);
            Console.WriteLine($"\n— Giới thiệu về vị trí {position.Name} —");
            Console.WriteLine(roleIntro);
            _History.Add(("assistant", roleIntro));

            // (4) PHỎNG VẤN CHUYÊN MÔN
            var questions = await _QTool.GetQuestionsAsync(position.Name, level, AgentConsts.MaxQuestions, ct);
            if (questions.Length == 0)
            {
                Console.WriteLine("Chưa có câu hỏi cho vị trí/level này. Hẹn gặp lại!");
                return;
            }

            var session = new InterviewSession
            {
                CandidateId = candidate.Id,
                PositionId = position.Id,
                Level = level,
                CreatedAt = DateTime.Now,
                Score = 0,
                Result = InterviewResult.None
            };
            _Db.InterviewSessions.Add(session);
            await _Db.SaveChangesAsync(ct);

            // double totalWeight = questions.Sum(q => q.Weight);
            double totalWeight = questions.Sum(q => Math.Max(1, q.Weight));

            foreach (var q in questions)
            {
                var askQ = await AskModelAsync($"Hỏi ứng viên ngắn gọn câu này: {q.Text}", ct);
                Console.WriteLine(askQ);
                _History.Add(("assistant", askQ));

                Console.Write("> ");
                var ans = Console.ReadLine() ?? "";
                _History.Add(("user", ans));

                var (sc, cm) = await _Scoring.ScoreAnswerSmartAsync(ans, q, ct);
                _Db.Answers.Add(new Answer
                {
                    SessionId = session.Id,
                    QuestionId = q.Id,
                    Content = ans,
                    Score = sc,
                    Comment = $"{cm} | Gợi ý: {q.ModelAnswer}",
                    CreatedAt = DateTime.Now
                });
                await _Db.SaveChangesAsync(ct);
                session.Score += sc;

                var feedback = await AskModelAsync(
                    $"Nhận xét 1-2 câu về câu trả lời trên (bám sát tiêu chí: {cm}). Vui lòng nhận xét chính xác thẳng thắng, trung thực, mang tính xây dựng chỉ bảo nếu câu trả lời sai sót(Tuyệt đối chỉ nhận xét mà không hỏi lại bất cứ câu gì thêm!)",
                    ct);
                Console.WriteLine($"→ Nhận xét tự động từ AI Agent: {feedback}\n");
                _History.Add(("assistant", feedback));
            }

            // (5)(6)(7) Tổng kết
            session.Score = totalWeight <= 0 ? 0 : Math.Round(session.Score * 100.0 / totalWeight, 2);
            session.Result = session.Score >= AgentConsts.PassThreshold ? InterviewResult.Pass : InterviewResult.Fail;
            await _Db.SaveChangesAsync(ct);

            var closing = await AskModelAsync(
                $"Tạo lời kết lịch sự, tích cực. Tổng điểm={session.Score:0.##}. " +
                $"Nếu Pass (>=60) hãy thông báo sẽ gửi kết quả qua email và đặt lịch vòng 2. " +
                $"Nếu Fail (<60) hãy phản hồi tế nhị, gợi ý cải thiện.",
                ct);
            Console.WriteLine($"\n===== KẾT THÚC =====\n{closing}\n");
            _History.Add(("assistant", closing));

            if (session.Result == InterviewResult.Pass)
            {
                var leader = _Matcher.FindLeaderForPosition(position.Id);
                if (leader == null)
                {
                    Console.WriteLine("Hiện tại chưa tìm thấy leader phù hợp. Cảm ơn bạn đã tham gia phỏng vấn cùng Asoft. Công ty sẽ liên hệ bạn trong thời gian sớm nhất!");
                    return;
                }

                var schedule = _Scheduler.CreateNextRoundSchedule(candidate.Id, leader.Id, position.Id);

                // ========== Email gửi cho ỨNG VIÊN ==========
                string subjectCandidate = $"[ASOFT] Thư mời phỏng vấn chuyên sâu– {position.Name}";
                string bodyCandidate = $@"
                Thân chào {candidate.FullName},

                Công ty Cổ phần ASOFT trân trọng mời Bạn tham dự buổi phỏng vấn vòng 2 cho vị trí **{position.Name}** theo thông tin sau:

                - Thời gian: {schedule.StartTime:HH:mm}, ngày {schedule.StartTime:dd/MM/yyyy}
                - Hình thức: Offline tại văn phòng
                - Địa điểm: Tầng 3, Tòa nhà JVPE, Đường số 13, Công viên phần mềm Quang Trung, Phường Trung Mỹ Tây, TP. HCM

                Buổi phỏng vấn vòng 2 sẽ được trực tiếp trao đổi cùng Leader {leader.FullName} để đánh giá chuyên sâu hơn về kiến thức, kỹ năng cũng như sự phù hợp với công việc.

                Bạn vui lòng phản hồi lại email này để xác nhận lịch hẹn. Nếu thời gian trên chưa phù hợp, Bạn có thể liên hệ ngay qua email: recruit@asoft.com.vn hoặc số điện thoại 0356 660 226 – Ms. Nguyệt để được hỗ trợ.

                Rất mong sớm được gặp và trao đổi cùng Bạn.

                Trân trọng,
                Phòng Nhân sự – ASOFT
                *** Lưu ý: Đây là email được gửi tự động từ AI Agent ***
                ";

                _Email.SendScheduleEmail(candidate.Email, subjectCandidate, bodyCandidate);


                // ========== Email gửi cho LEADER ==========
                string subjectLeader = $"[ASOFT] Thông báo lịch phỏng vấn ứng viên – {position.Name}";
                string bodyLeader = $@"
                Kính gửi {leader.FullName},

                Phòng Nhân sự xin thông báo lịch phỏng vấn vòng 2 của ứng viên **{candidate.FullName}** cho vị trí **{position.Name}**:

                - Thời gian: {schedule.StartTime:HH:mm}, ngày {schedule.StartTime:dd/MM/yyyy}
                - Ứng viên: {candidate.FullName} – Email: {candidate.Email}, SĐT: {candidate.Phone}
                - Hình thức: Offline tại văn phòng
                - Địa điểm: Tầng 3, Tòa nhà JVPE, Đường số 13, Công viên phần mềm Quang Trung, Phường Trung Mỹ Tây, TP. HCM

                Đề nghị {leader.FullName} sắp xếp thời gian để tham gia phỏng vấn và đánh giá ứng viên. 
                Nếu có thay đổi hoặc cần hỗ trợ thêm, vui lòng phản hồi lại cho phòng Nhân sự.

                Trân trọng,
                Phòng Nhân sự – ASOFT
                *** Lưu ý: Đây là email được gửi tự động từ AI Agent ***
                ";

                _Email.SendScheduleEmail(leader.Email, subjectLeader, bodyLeader);


                Console.WriteLine("✅ Đã đặt lịch vòng 2 & gửi email (demo). Cảm ơn bạn đã phỏng vấn cùng Asoft!");
            }
            else
            {
                Console.WriteLine("Cảm ơn bạn đã dành thời gian. Chúc bạn một ngày tốt lành và nhiều cơ hội phù hợp trong tương lai!");
            }
        }

        // ==================== HELPERS (LLM/DB) ====================

        private async Task<string> AskModelAsync(string userMessage, CancellationToken ct)
        {
            var reply = await _Llm.CompleteAsync(SysPrompt, _History, userMessage, ct);
            await Task.Delay(ThrottleDelayMs, ct);
            return reply;
        }

        private async Task<List<string>> GetActivePositionsAsync(CancellationToken ct)
        {
            var names = await _Db.PositionsInterview
                                 .AsNoTracking()
                                 .Select(p => p.Name)
                                 .ToListAsync(ct);

            return names
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // ===== LLM: trích xuất họ tên/email/phone JSON (im lặng nếu hỏng) =====
private async Task<(string? name, string? email, string? phone)> NormalizeContactByLlmAsync(
    string userInput,
    CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(userInput)) return (null, null, null);

    // System role rất chặt: trả về JSON thuần, không giải thích
    var sys = @"Bạn là bộ trích xuất liên hệ.
- Nhiệm vụ: từ văn bản tự do (tiếng Việt, có thể văn nói), trích xuất:
  name  = họ tên đầy đủ (không kèm từ 'tên', 'là', ...),
  email = địa chỉ email hợp lệ (nếu có),
  phone = số điện thoại dạng số (ưu tiên VN: +84xxxxxxxx hoặc 0xxxxxxxx).
- TRẢ VỀ JSON THUẦN: {""name"":""..."",""email"":""..."",""" + "phone" + @""":""...""}
- Nếu trường nào không có, để chuỗi rỗng.
- Tuyệt đối không thêm giải thích.";

    var user = $"Văn bản: \"{userInput}\". Hãy trả về đúng JSON nói trên.";

    var resp = await _Llm.CompleteAsync(sys, new List<(string, string)>(), user, ct);
    await Task.Delay(ThrottleDelayMs, ct);

    try
    {
        using var doc = JsonDocument.Parse(resp);
        var r = doc.RootElement;
        string? n = r.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
        string? e = r.TryGetProperty("email", out var eEl) ? eEl.GetString() : null;
        string? p = r.TryGetProperty("phone", out var pEl) ? pEl.GetString() : null;

        n = CleanName(n);
        e = CleanEmail(e);
        p = CleanPhone(p);
        return (EmptyToNull(n), EmptyToNull(e), EmptyToNull(p));
    }
    catch
    {
        // Không in cảnh báo; fallback ở dưới
        return (null, null, null);
    }
}

// ===== Fallback: tách theo từ-khóa tiếng Việt + regex =====

// chuẩn hoá tên: bỏ từ 'tên', 'là', dấu câu, khoảng trắng dư…
private static string CleanName(string? s)
{
    if (string.IsNullOrWhiteSpace(s)) return string.Empty;
    var x = s.Trim();

    // bỏ nhãn phổ biến
    x = Regex.Replace(x, @"\b(tên|họ.?tên|ho.?ten|name|là|la)\b", "", RegexOptions.IgnoreCase);
    // bỏ dấu câu lẻ
    x = Regex.Replace(x, @"[,:;|]+", " ");
    // giữ chữ cái & khoảng trắng
    x = Regex.Replace(x, @"[^\p{L}\s]", " ");
    x = Regex.Replace(x, @"\s+", " ").Trim();

    // loại chuỗi quá ngắn/vô nghĩa
    return x.Length >= 2 ? x : string.Empty;
}

private static string CleanEmail(string? s)
{
    if (string.IsNullOrWhiteSpace(s)) return string.Empty;
    s = s.Trim();
    // chấp nhận email hợp lệ chung (gmailgmail.com vẫn hợp lệ về mặt cú pháp)
    var m = Regex.Match(s, @"^[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}$", RegexOptions.IgnoreCase);
    return m.Success ? m.Value : string.Empty;
}

private static string CleanPhone(string? s)
{
    if (string.IsNullOrWhiteSpace(s)) return string.Empty;
    var x = s.Replace(" ", "").Replace("-", "").Trim();
    // Ưu tiên +84… hoặc 0xxxxxxxxx (9-11 số)
    var m = Regex.Match(x, @"^(?:\+?84\d{8,11}|0\d{8,11})$");
    if (m.Success) return m.Value;

    // fallback: chuỗi số dài 8–15
    m = Regex.Match(x, @"^\+?\d{8,15}$");
    return m.Success ? m.Value : string.Empty;
}

private static string EmptyToNull(string? s) => string.IsNullOrWhiteSpace(s) ? null! : s!;

// ======================= PARSE & NORMALIZE CONTACT =======================
private static void UpdateContactInfo(string input, ref string name, ref string email, ref string phone)
{
    if (string.IsNullOrWhiteSpace(input)) return;

    // 1) Trích email trước (dễ nhất)
    if (string.IsNullOrWhiteSpace(email))
    {
        var me = Regex.Match(input, @"([A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,})", RegexOptions.IgnoreCase);
        if (me.Success) email = me.Groups[1].Value.Trim();
    }

    // 2) Trích phone
    if (string.IsNullOrWhiteSpace(phone))
    {
        var normalized = input.Replace(" ", "").Replace("-", "");
        // ưu tiên +84/0
        var mp = Regex.Match(normalized, @"(\+?84\d{8,11}|0\d{8,11})");
        if (!mp.Success) mp = Regex.Match(normalized, @"(\+?\d{8,15})");
        if (mp.Success) phone = mp.Groups[1].Value;
    }

    // 3) Tách cụm, ưu tiên cụm có từ 'tên'
    if (string.IsNullOrWhiteSpace(name))
    {
        var parts = input.Split(new[] { ',', ';', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim()).ToList();

        // cụm có từ 'tên'
        var namePart = parts.FirstOrDefault(p => Regex.IsMatch(p, @"\btên\b|\bhọ.?tên\b|ho.?ten\b|name\b", RegexOptions.IgnoreCase))
                    ?? parts.FirstOrDefault();

        // loại bỏ email/phone khỏi cụm tên
        if (!string.IsNullOrEmpty(email)) namePart = namePart?.Replace(email, "", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(phone)) namePart = namePart?.Replace(phone, "", StringComparison.OrdinalIgnoreCase);

        var cleaned = CleanName(namePart);
        if (!string.IsNullOrWhiteSpace(cleaned)) name = cleaned;
    }
}


        // LLM kiểm tra có nhắc tới 1 vị trí trong danh sách (chấp nhận typo, văn nói)
        private async Task<string> CheckContainsPositionByLlmAsync(string userAnswer, IEnumerable<string> allowedPositions, CancellationToken ct)
        {
            var sys = @"Bạn là bộ đối sánh vị trí ứng tuyển.
        - Nhiệm vụ: từ câu trả lời tự do (có thể có lỗi chính tả/tiếng lóng), xác định vị trí gần nhất trong danh sách cho phép.
        - Chỉ trả về JSON:
        { ""match"": true|false, ""normalized"": ""<tên VỊ TRÍ đúng như trong danh sách hoặc rỗng>"" }
        - Bỏ qua hư từ như 'ạ', 'dạ', 'mình', 'em', 'anh', ..., ký tự thừa.
        - Ưu tiên khớp gần đúng (fuzzy) và đồng nghĩa hiển nhiên (ví dụ Enginner → Engineer). Không suy đoán ngoài danh sách.";
            var list = string.Join(", ", allowedPositions);
            var user = $"Danh sách cho phép: [{list}]\nCâu trả lời: \"{userAnswer}\".\nTrả về JSON theo hướng dẫn.";

            var resp = await _Llm.CompleteAsync(sys, new List<(string, string)>(), user, ct);
            await Task.Delay(ThrottleDelayMs, ct);

            try
            {
                using var doc = JsonDocument.Parse(resp);
                var root = doc.RootElement;

                bool match = root.TryGetProperty("match", out var mEl) && mEl.ValueKind == JsonValueKind.True;
                string normalized = root.TryGetProperty("normalized", out var nEl) ? (nEl.GetString() ?? "") : "";

                if (match && !string.IsNullOrWhiteSpace(normalized))
                {
                    var picked = allowedPositions.FirstOrDefault(ap =>
                        string.Equals(ap?.Trim(), normalized?.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(picked)) return picked.Trim();
                }
            }
            catch { /* ignore */ }

            // Fallback local fuzzy (bỏ dấu + Levenshtein + bao hàm)
            return FuzzyBestMatch(userAnswer, allowedPositions);
        }

        // LLM chuẩn hoá level → "1"/"2"/"3"
        private async Task<string> CheckLevelByLlmAsync(string userAnswer, CancellationToken ct)
        {
            var sys = @"Bạn là bộ chuẩn hoá level ứng viên.
            - Suy ra level 1/2/3 từ câu trả lời tự do (fresher=1, junior=2, senior=3; chấp nhận văn nói như 'fresher á', 'em junior ạ').
            - Trả về JSON: { ""level"": ""1|2|3|"" } (chuỗi rỗng nếu không xác định).";
            var user = $"Câu trả lời: \"{userAnswer}\". Trả về JSON theo yêu cầu.";

            var resp = await _Llm.CompleteAsync(sys, new List<(string, string)>(), user, ct);
            await Task.Delay(ThrottleDelayMs, ct);

            try
            {
                using var doc = JsonDocument.Parse(resp);
                var root = doc.RootElement;
                var lv = root.TryGetProperty("level", out var el) ? (el.GetString() ?? "") : "";
                lv = NormalizeLevel(lv);
                if (!string.IsNullOrWhiteSpace(lv)) return lv;
            }
            catch { /* ignore */ }

            // Fallback local từ văn nói
            return NormalizeLevel(userAnswer);
        }

        private async Task<string> AskPositionWithVerificationAsync(List<string> activePositions, CancellationToken ct)
        {
            Console.WriteLine("Bạn muốn ứng tuyển vào vị trí nào trong danh sách trên? Vui lòng nhập tên vị trí (có thể gõ gần đúng).");
            Console.Write("> ");
            var ans1 = (Console.ReadLine() ?? "").Trim();
            _History.Add(("user", ans1));

            var pos1 = await CheckContainsPositionByLlmAsync(ans1, activePositions, ct);
            if (!string.IsNullOrWhiteSpace(pos1)) return pos1;

            Console.WriteLine($"Mình chưa rõ vị trí bạn chọn. Bạn vui lòng nhập lại (có thể gõ gần đúng):");
            Console.Write("> ");
            var ans2 = (Console.ReadLine() ?? "").Trim();
            _History.Add(("user", ans2));

            var pos2 = await CheckContainsPositionByLlmAsync(ans2, activePositions, ct);
            return string.IsNullOrWhiteSpace(pos2) ? string.Empty : pos2;
        }

        private async Task<string> AskLevelWithVerificationAsync(CancellationToken ct)
        {
            Console.WriteLine("Level hiện tại của bạn là gì? (1=Fresher, 2=Junior, 3=Senior). Bạn có thể trả lời 1/2/3 hoặc fresher/junior/senior.");
            Console.Write("> ");
            var ans1 = (Console.ReadLine() ?? "").Trim();
            _History.Add(("user", ans1));

            var lv1 = await CheckLevelByLlmAsync(ans1, ct);
            if (!string.IsNullOrWhiteSpace(lv1)) return lv1;

            Console.WriteLine("Chưa xác định được level. Vui lòng trả lời 1/2/3 hoặc fresher/junior/senior.");
            Console.Write("> ");
            var ans2 = (Console.ReadLine() ?? "").Trim();
            _History.Add(("user", ans2));

            var lv2 = await CheckLevelByLlmAsync(ans2, ct);
            return string.IsNullOrWhiteSpace(lv2) ? string.Empty : lv2;
        }

        // ---------- DB: tìm Position theo tên chuẩn hoá (fuzzy phòng trường hợp khác biệt) ----------
        private async Task<Position?> FindPositionEntityAsync(string posName, CancellationToken ct)
        {
            var all = await _Db.Positions.AsNoTracking().ToListAsync(ct);
            if (all.Count == 0) return null;

            // ưu tiên khớp không phân biệt hoa thường
            var direct = all.FirstOrDefault(p => string.Equals(p.Name?.Trim(), posName?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (direct != null) return direct;

            // fuzzy theo tên trong Positions
            var best = FuzzyBestMatch(posName, all.Select(p => p.Name ?? "").ToList());
            if (!string.IsNullOrWhiteSpace(best))
            {
                var match = all.FirstOrDefault(p => string.Equals(p.Name?.Trim(), best.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            return null;
        }

        
        

        // ======================= FUZZY HELPERS (VN-friendly) =======================
        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var norm = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in norm)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string Canonicalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var noAcc = RemoveDiacritics(s);
            var oneSp = Regex.Replace(noAcc.Trim(), @"\s+", " ");
            return oneSp.ToLowerInvariant();
        }

        private static int Levenshtein(string a, string b)
        {
            var n = a.Length; var m = b.Length;
            var d = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            return d[n, m];
        }

        private static string FuzzyBestMatch(string input, IEnumerable<string> candidates)
        {
            var canonIn = Canonicalize(input);
            if (string.IsNullOrWhiteSpace(canonIn)) return string.Empty;

            string best = string.Empty;
            int bestScore = int.MaxValue;
            foreach (var c in candidates)
            {
                var canonC = Canonicalize(c);
                if (string.IsNullOrWhiteSpace(canonC)) continue;

                // Ưu tiên bao hàm
                if (canonC.Contains(canonIn) || canonIn.Contains(canonC)) return c.Trim();

                // Levenshtein (ngưỡng lỏng)
                int dist = Levenshtein(canonIn, canonC);
                if (dist < bestScore)
                {
                    bestScore = dist;
                    best = c.Trim();
                }
            }

            // Ngưỡng: khoảng 30% độ dài hoặc <= 3 ký tự khác (tuỳ chuỗi)
            int threshold = Math.Min(3, Math.Max(1, (int)Math.Round(canonIn.Length * 0.3)));
            return bestScore <= threshold ? best : string.Empty;
        }

        private static string NormalizeLevel(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var s = Canonicalize(input);

            // số trực tiếp
            if (int.TryParse(s, out var n) && n >= 1 && n <= 3) return n.ToString();

            // văn nói
            if (Regex.IsMatch(s, @"\bfresher\b")) return "1";
            if (Regex.IsMatch(s, @"\bjun(ior)?\b")) return "2";
            if (Regex.IsMatch(s, @"\bsen(ior)?\b")) return "3";

            return string.Empty;
        }

        // Validators (còn dùng ở chỗ khác)
        private static bool ValidatePosition(string input, IEnumerable<string> allowed)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var x = input.Trim();
            foreach (var p in allowed)
                if (string.Equals(p?.Trim(), x, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool ValidateLevelFlexible(string input)
            => ParseLevelFlexible(input) is >= 1 and <= 3;

        private static int ParseLevelFlexible(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;
            var s = Canonicalize(input);

            if (int.TryParse(s, out var n) && n is >= 1 and <= 3) return n;

            if (Regex.IsMatch(s, @"\bfresher\b")) return 1;
            if (Regex.IsMatch(s, @"\bjun(ior)?\b")) return 2;
            if (Regex.IsMatch(s, @"\bsen(ior)?\b")) return 3;

            return 0;
        }
    }
}
