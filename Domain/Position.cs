using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_Agent_Basic.Domain
{
    [Table("positions")]
    public sealed class Position
    {
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("required_skills_csv")]
        public string RequiredSkillsCsv { get; set; } = string.Empty;

        public ICollection<InterviewQuestion> Questions { get; set; } = new List<InterviewQuestion>();
        public ICollection<Leader> Leaders { get; set; } = new List<Leader>();
    }
}
