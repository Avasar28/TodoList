using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TodoListApp.Models;

namespace TodoListApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<PdfFileHistory> PdfFiles { get; set; }
        public DbSet<SystemFeature> SystemFeatures { get; set; }
        public DbSet<UserFeatureAccess> UserFeatures { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<Goal> Goals { get; set; }
        public DbSet<GoalSchedule> GoalSchedules { get; set; }
        public DbSet<UserAchievement> UserAchievements { get; set; }
        public DbSet<GoalSubTask> GoalSubTasks { get; set; }
        public DbSet<UserWebAuthnCredential> WebAuthnCredentials { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // Additional configurations if needed
        }
    }
}
