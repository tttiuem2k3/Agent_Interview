using AI_Agent_Basic.Domain;
using Microsoft.EntityFrameworkCore;

namespace AI_Agent_Basic.Infrastructure
{
    /// <summary>DbContext quản lý dữ liệu phỏng vấn</summary>
    public sealed class AgentDbContext : DbContext
    {
        public DbSet<Position> Positions => Set<Position>();
        public DbSet<InterviewQuestion> InterviewQuestions => Set<InterviewQuestion>();
        public DbSet<Leader> Leaders => Set<Leader>();
        public DbSet<Candidate> Candidates => Set<Candidate>();
        public DbSet<InterviewSession> InterviewSessions => Set<InterviewSession>();
        public DbSet<Answer> Answers => Set<Answer>();
        public DbSet<Schedule> Schedules => Set<Schedule>();
        public DbSet<PositionInterview> PositionsInterview => Set<PositionInterview>();
        protected override void OnConfiguring(DbContextOptionsBuilder pOptionsBuilder)
        {
            if (!pOptionsBuilder.IsConfigured)
            {
                pOptionsBuilder.UseMySql(
                    AgentConsts.MySqlConnection,
                    new MySqlServerVersion(AgentConsts.MySqlServerVersion)
                );
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Áp dụng mặc định UTF-8 cho toàn DB context
            modelBuilder
                .UseCollation("utf8mb4_unicode_ci")
                .HasCharSet("utf8mb4");

            // Ví dụ map cột tên ứng viên (nếu đã có entity)
            modelBuilder.Entity<Candidate>(e =>
            {
                e.Property(x => x.FullName)
                .HasMaxLength(255)
                .UseCollation("utf8mb4_unicode_ci")
                .HasCharSet("utf8mb4");
            });

            modelBuilder.Entity<Answer>(e =>
            {
                e.Property(x => x.Content)
                .HasMaxLength(255)
                .UseCollation("utf8mb4_unicode_ci")
                .HasCharSet("utf8mb4");
            });
            // Map tên bảng chính (nếu chưa có)
            modelBuilder.Entity<Position>().ToTable("positions");
            modelBuilder.Entity<Leader>().ToTable("leaders");
            modelBuilder.Entity<InterviewQuestion>().ToTable("interview_questions");
            modelBuilder.Entity<Candidate>().ToTable("candidates");
            modelBuilder.Entity<InterviewSession>().ToTable("interview_sessions");
            modelBuilder.Entity<Answer>().ToTable("answers");
            modelBuilder.Entity<Schedule>().ToTable("schedules");
            modelBuilder.Entity<PositionInterview>().ToTable("positions_interview");
            // Enum -> string (khớp ENUM('None','Pass','Fail'))
            modelBuilder.Entity<InterviewSession>()
                .Property(s => s.Result)
                .HasConversion<string>()
                .HasColumnType("ENUM('None','Pass','Fail')");

            // Many-to-many: dùng bảng leaders_positions (leader_id, position_id)
            // join table: leaders_positions
            modelBuilder.Entity<Leader>()
            .HasMany(l => l.Positions)
            .WithMany(p => p.Leaders)
            .UsingEntity<Dictionary<string, object>>(
                "leaders_positions",                                  // hoặc "positions_leaders" nếu DB của bạn dùng tên đó
                j => j.HasOne<Position>().WithMany().HasForeignKey("position_id"),
                j => j.HasOne<Leader>().WithMany().HasForeignKey("leader_id"),
                j =>
                {
                    j.ToTable("leaders_positions");                   // đổi đúng tên bảng join mà DB đang có
                    j.HasKey("leader_id", "position_id");
                });
        }



        /// <summary>Tạo DB & áp dụng migrations</summary>
        public static void EnsureInitWithMigrations()
        {
            using var db = new AgentDbContext();
            db.Database.Migrate();
        }
    }
}
