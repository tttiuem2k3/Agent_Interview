using System.ComponentModel.DataAnnotations.Schema;

namespace AI_Agent_Basic.Domain
{
    [Table("candidates")]
    public sealed class Candidate
    {
        public int Id { get; set; }

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("phone")]
        public string? Phone { get; set; }
    }
}
