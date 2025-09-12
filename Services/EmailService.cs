using System;

namespace AI_Agent_Basic.Services
{
    /// <summary>Gửi email (demo: in nội dung ra console)</summary>
    public sealed class EmailService
    {
        public void SendScheduleEmail(string pTo, string pSubject, string pBody)
        {
            Console.WriteLine("\n--- EMAIL DEMO ---");
            Console.WriteLine($"To: {pTo}");
            Console.WriteLine($"Subject: {pSubject}");
            Console.WriteLine(pBody);
            Console.WriteLine("--- END EMAIL ---\n");

            // Triển khai SMTP thật nếu cần:
            // using var client = new SmtpClient("smtp.server.vn", 587) { EnableSsl = true, Credentials = new NetworkCredential("user","pass") };
            // var mail = new MailMessage(AgentConsts.MailFrom, pTo, pSubject, pBody);
            // client.Send(mail);
        }
    }
}
