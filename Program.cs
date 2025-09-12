using System;
using System.Threading.Tasks;
using AI_Agent_Basic.Agent;
using AI_Agent_Basic.Infrastructure;
using AI_Agent_Basic.Services;
using AI_Agent_Basic.Tools;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;


public static class Program
{
    public static async Task Main()
    {
        
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding  = Encoding.Unicode;

        // 1) Lấy API key từ ENV hoặc .NET User Secrets
        // Ưu tiên: GEMINI_API_KEY, fallback: GOOGLE_API_KEY (một số SDK dùng tên này)
        var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                        ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");

        if (string.IsNullOrWhiteSpace(geminiKey))
        {
            throw new InvalidOperationException(
                "Missing GEMINI_API_KEY (or GOOGLE_API_KEY). " +
                "Set it via environment variable or `dotnet user-secrets`."
            );
        }

        // 2) Khởi tạo LLM client
        ILlmClient llm = new GeminiLlmClient(
            geminiKey!,
            "gemini-2.0-flash"
        );

        // 3) DB migrations nếu dùng EF
        AgentDbContext.EnsureInitWithMigrations();

        var db      = new AgentDbContext();
        var qTool   = new QuestionBankTool(db);
        var scoring = new ScoringService(llm);
        var matcher = new LeaderMatchingService(db);
        var sched   = new SchedulingService(db);
        var email   = new EmailService();

        var agent = new AgentOrchestrator(llm, db, qTool, scoring, matcher, sched, email);
        await agent.RunAsync(default);

    }
}
