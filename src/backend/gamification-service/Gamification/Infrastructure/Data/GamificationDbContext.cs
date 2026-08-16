using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Gamification.Features.Achievements.Models;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.League.Models;
using Sellevate.Gamification.Identity;

namespace Sellevate.Gamification.Infrastructure.Data;

public sealed class GamificationDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public GamificationDbContext(DbContextOptions<GamificationDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
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

        // Phase 40.13. Convenience, not security — the boundary is the RLS policy the
        // AddOrganizationId migration installs (docs/TENANCY/TENANCY.md §1.4-§1.5).
        //
        // Every tenant-scoped entity is listed one by one, because EF does not inherit query
        // filters through navigations. GamificationTenancyModelTests walks the model and fails the
        // build if an entity grows an OrganizationId without appearing here.
        //
        // Strict equality throughout, with no `IS NULL OR` branch: gamification-db has no global
        // content library. The tables that ARE platform-global — Achievements and LeagueTiers
        // (catalogues), GamificationSettings, StreakMilestones and ExerciseTypeRewards
        // (installation-wide configuration), UserReplicas (cross-org identities, §4.2) and
        // OutboxMessages (read only by the system-mode relay) — carry no OrganizationId at all,
        // which is what keeps "a row with no organization is invisible, not shared" true here.
        modelBuilder.Entity<UserExperiencePointsRecord>()
            .HasQueryFilter(record => _tenantContext.IsPlatformWide || record.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<UserStreak>()
            .HasQueryFilter(streak => _tenantContext.IsPlatformWide || streak.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<UserAchievement>()
            .HasQueryFilter(userAchievement => _tenantContext.IsPlatformWide || userAchievement.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<UserLearningProgress>()
            .HasQueryFilter(progress => _tenantContext.IsPlatformWide || progress.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<League>()
            .HasQueryFilter(league => _tenantContext.IsPlatformWide || league.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<LeagueMembership>()
            .HasQueryFilter(membership => _tenantContext.IsPlatformWide || membership.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<LeagueSettings>()
            .HasQueryFilter(settings => _tenantContext.IsPlatformWide || settings.OrganizationId == _tenantContext.OrganizationId);
    }
}
