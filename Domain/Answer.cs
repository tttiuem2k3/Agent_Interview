using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_Agent_Basic.Domain
{
    [Table("answers")]
    public sealed class Answer
    {
        public int Id { get; set; }

        [Column("session_id")]
        public int SessionId { get; set; }

        [Column("question_id")]
        public int QuestionId { get; set; }

        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Column("score")]
        public double Score { get; set; }

        [Column("comment")]
        public string Comment { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        public InterviewSession? Session { get; set; }
        public InterviewQuestion? Question { get; set; }
    }
}
