using System.ComponentModel.DataAnnotations.Schema;

namespace AI_Agent_Basic.Domain
{
    // Map tới bảng positions_interview (id, name)
    [Table("positions_interview")]
    public sealed class PositionInterview
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }
}
