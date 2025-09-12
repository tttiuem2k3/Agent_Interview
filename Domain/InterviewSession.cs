using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_Agent_Basic.Domain
{
    [Table("interview_sessions")]
    public sealed class InterviewSession
    {
        public int Id { get; set; }

        [Column("candidate_id")]
        public int CandidateId { get; set; }

        [Column("position_id")]
        public int PositionId { get; set; }

        [Column("level")]
        public int Level { get; set; }

        [Column("score")]
        public double Score { get; set; }

        // DB là ENUM('None','Pass','Fail') → ta map enum sang string trong DbContext
        [Column("result")]
        public InterviewResult Result { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        public Candidate? Candidate { get; set; }
        public Position? Position { get; set; }
        public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    }
}
