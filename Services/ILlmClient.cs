using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI_Agent_Basic.Services
{
    public interface ILlmClient
    {
        Task<string> CompleteAsync(
            string systemPrompt,
            IEnumerable<(string role, string content)> history,
            string userMessage,
            CancellationToken ct);
    }
}
