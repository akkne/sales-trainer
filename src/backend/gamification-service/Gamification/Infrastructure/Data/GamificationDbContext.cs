using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.Gamification.Features.Achievements.Models;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.League.Models;
using Sellevate.Gamification.Identity;

namespace Sellevate.Gamification.Infrastructure.Data;

public sealed class GamificationDbContext : DbContext
{
    public GamificationDbContext(DbContextOptions<GamificationDbContext> options) : base(options)
    {
    }

    public DbSet<UserExperiencePointsRecord> UserExperiencePointsRecords => Set<UserExperiencePointsRecord>();
    public DbSet<UserStreak> UserStreaks => Set<UserStreak>();
    public DbSet<GamificationSettings> GamificationSettings => Set<GamificationSettings>();
    public DbSet<ExerciseTypeReward> ExerciseTypeRewards => Set<ExerciseTypeReward>();
    public DbSet<StreakMilestone> StreakMilestones => Set<StreakMilestone>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<UserLearningProgress> UserLearningProgressRecords => Set<UserLearningProgress>();
    public DbSet<League> Leagues => Set<League>();
    public DbSet<LeagueTier> LeagueTiers => Set<LeagueTier>();
    public DbSet<LeagueMembership> LeagueMemberships => Set<LeagueMembership>();
    public DbSet<LeagueSettings> LeagueSettings => Set<LeagueSettings>();
    public DbSet<UserReplica> UserReplicas => Set<UserReplica>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserExperiencePointsRecordEntityConfiguration());
        modelBuilder.ApplyConfiguration(new UserStreakEntityConfiguration());
        modelBuilder.ApplyConfiguration(new GamificationSettingsEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ExerciseTypeRewardEntityConfiguration());
        modelBuilder.ApplyConfiguration(new StreakMilestoneEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AchievementEntityConfiguration());
        modelBuilder.ApplyConfiguration(new UserAchievementEntityConfiguration());
        modelBuilder.ApplyConfiguration(new UserLearningProgressEntityConfiguration());
        modelBuilder.ApplyConfiguration(new LeagueEntityConfiguration());
        modelBuilder.ApplyConfiguration(new LeagueTierEntityConfiguration());
        modelBuilder.ApplyConfiguration(new LeagueMembershipEntityConfiguration());
        modelBuilder.ApplyConfiguration(new LeagueSettingsEntityConfiguration());
        modelBuilder.ApplyConfiguration(new UserReplicaEntityConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageEntityConfiguration());
    }
}
