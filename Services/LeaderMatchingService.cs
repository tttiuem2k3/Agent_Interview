using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using AI_Agent_Basic.Domain;
using AI_Agent_Basic.Infrastructure;

namespace AI_Agent_Basic.Services
{
    /// <summary>
    /// Chọn Leader phù hợp với Position.
    /// Dựa trên quan hệ many-to-many Leader.Positions (EF navigation) nên không phụ thuộc tên bảng join.
    /// Có load-balancing nhẹ theo số lịch sắp tới trong bảng Schedules.
    /// </summary>
    public sealed class LeaderMatchingService
    {
        private readonly AgentDbContext _db;

        public LeaderMatchingService(AgentDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Tìm leader cho Position theo Id. Ưu tiên leader có ít lịch sắp tới hơn.
        /// </summary>
        /// <param name="positionId">Id của vị trí.</param>
        /// <returns>Leader phù hợp hoặc null nếu không có.</returns>
        public Leader? FindLeaderForPosition(int positionId)
        {
            // Kiểm tra position có tồn tại không
            var position = _db.Positions.AsNoTracking().FirstOrDefault(p => p.Id == positionId);
            if (position == null)
                return null;

            // Lấy các leader có thể phỏng vấn position này (thông qua navigation property)
            var leaders = _db.Leaders
                .Include(l => l.Positions)
                .Where(l => l.Positions.Any(p => p.Id == positionId))
                .AsNoTracking()
                .ToList();

            if (leaders.Count == 0)
                return null;

            // Load-balancing: sắp xếp theo số lịch sắp tới (ít lịch -> ưu tiên)
            // Bạn có thể chỉnh "window" thời gian nếu muốn (vd. 7 ngày tới).
            var now = DateTime.Now;
            var leaderLoads = leaders
                .Select(l => new
                {
                    Leader = l,
                    Load = _db.Schedules.AsNoTracking()
                        .Count(s => s.LeaderId == l.Id && s.StartTime >= now)
                })
                .OrderBy(x => x.Load)
                .ThenBy(x => x.Leader.Id)
                .ToList();

            return leaderLoads.First().Leader;
        }

        /// <summary>
        /// Tìm leader cho Position theo tên vị trí (so khớp đúng tên trong bảng positions).
        /// </summary>
        /// <param name="positionName">Tên vị trí (ví dụ: "AI Engineer").</param>
        /// <returns>Leader phù hợp hoặc null nếu không có.</returns>
        public Leader? FindLeaderForPositionName(string positionName)
        {
            if (string.IsNullOrWhiteSpace(positionName))
                return null;

            var position = _db.Positions
                .AsNoTracking()
                .FirstOrDefault(p => p.Name == positionName.Trim());

            return position == null ? null : FindLeaderForPosition(position.Id);
        }
    }
}
