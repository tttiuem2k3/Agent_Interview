using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_Agent_Basic.Domain
{
    [Table("schedules")]
    public sealed class Schedule
    {
        public int Id { get; set; }

        [Column("candidate_id")]
        public int CandidateId { get; set; }

        [Column("leader_id")]
        public int LeaderId { get; set; }

        [Column("position_id")]
        public int PositionId { get; set; }

        [Column("start_time")]
        public DateTime StartTime { get; set; }

        [Column("note")]
        public string Note { get; set; } = string.Empty;

        // (Có cột created_at trong DB; nếu cần bạn có thể thêm property và [Column("created_at")] )
    }
}
