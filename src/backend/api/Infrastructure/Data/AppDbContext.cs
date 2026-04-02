using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Auth;
using SalesTrainer.Api.Features.Gamification;
using SalesTrainer.Api.Features.League;
using SalesTrainer.Api.Features.Lessons;
using SalesTrainer.Api.Features.Onboarding;
using SalesTrainer.Api.Features.Reference;
using SalesTrainer.Api.Features.SkillTree;

namespace SalesTrainer.Api.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<UserSkillProgress> UserSkillProgressRecords => Set<UserSkillProgress>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<UserLessonProgress> UserLessonProgressRecords => Set<UserLessonProgress>();
    public DbSet<UserExerciseAttempt> UserExerciseAttempts => Set<UserExerciseAttempt>();
    public DbSet<UserStreak> UserStreaks => Set<UserStreak>();
    public DbSet<UserXp> UserXpRecords => Set<UserXp>();
    public DbSet<League> Leagues => Set<League>();
    public DbSet<LeagueMembership> LeagueMemberships => Set<LeagueMembership>();
    public DbSet<ReferenceMaterial> ReferenceMaterials => Set<ReferenceMaterial>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
