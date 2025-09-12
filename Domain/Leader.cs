using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_Agent_Basic.Domain
{
    [Table("leaders")]
    public sealed class Leader
    {
        public int Id { get; set; }

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = "quangtrung04122k3@gmail.com";

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        public ICollection<Position> Positions { get; set; } = new List<Position>();
    }
}
