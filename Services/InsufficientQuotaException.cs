namespace AI_Agent_Basic.Services
{
    /// <summary>
    /// Ngoại lệ riêng để báo lỗi khi hết quota (Gemini / OpenAI).
    /// </summary>
    public sealed class InsufficientQuotaException : System.Exception
    {
        public InsufficientQuotaException(string message) : base(message) { }
    }
}
