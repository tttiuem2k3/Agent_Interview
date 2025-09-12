using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI_Agent_Basic.Domain;
using AI_Agent_Basic.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AI_Agent_Basic.Tools
{
    public sealed class QuestionBankTool
    {
        private readonly AgentDbContext _Db;
        public QuestionBankTool(AgentDbContext db) { _Db = db; }

        public async Task<InterviewQuestion[]> GetQuestionsAsync(string positionName, int level, int limit, CancellationToken ct)
        {
            var pos = await _Db.Positions.AsNoTracking().FirstOrDefaultAsync(p => p.Name == positionName, ct);
            if (pos == null) return System.Array.Empty<InterviewQuestion>();

            return await _Db.InterviewQuestions.AsNoTracking()
                .Where(q => q.PositionId == pos.Id && q.Level == level)
                .OrderBy(q => q.Id)
                .Take(limit)
                .ToArrayAsync(ct);
        }
    }
}
