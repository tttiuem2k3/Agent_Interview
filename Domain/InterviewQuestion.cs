using System.ComponentModel.DataAnnotations.Schema;

namespace AI_Agent_Basic.Domain
{
    [Table("interview_questions")]
    public sealed class InterviewQuestion
    {
        public int Id { get; set; }

        [Column("position_id")]
        public int PositionId { get; set; }

        [Column("level")]
        public int Level { get; set; }

        [Column("text")]
        public string Text { get; set; } = string.Empty;

        [Column("weight")]
        public double Weight { get; set; }

        [Column("keywords_csv")]
        public string KeywordsCsv { get; set; } = string.Empty;

        [Column("model_answer")]
        public string ModelAnswer { get; set; } = string.Empty;

        public Position? Position { get; set; }
    }
}
