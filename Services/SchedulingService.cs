using System;
using AI_Agent_Basic.Domain;
using AI_Agent_Basic.Infrastructure;

namespace AI_Agent_Basic.Services
{
    /// <summary>Tạo lịch phỏng vấn vòng sau</summary>
    public sealed class SchedulingService
    {
        private readonly AgentDbContext _Db;

        public SchedulingService(AgentDbContext pDb)
        {
            _Db = pDb;
        }

        public Schedule CreateNextRoundSchedule(int pCandidateId, int pLeaderId, int pPositionId)
        {
            DateTime start = DateTime.Now.Date.AddDays(2).AddHours(10);
            var schedule = new Schedule
            {
                CandidateId = pCandidateId,
                LeaderId = pLeaderId,
                PositionId = pPositionId,
                StartTime = start,
                Note = "Phỏng vấn vòng 2 (tự động tạo bởi AI Agent)"
            };
            _Db.Schedules.Add(schedule);
            _Db.SaveChanges();
            return schedule;
        }
    }
}
