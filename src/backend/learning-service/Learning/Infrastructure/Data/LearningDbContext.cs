using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Features.DailyQuotes.Models;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Reference.Models;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Features.Techniques.Models;
using Sellevate.Learning.Identity;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class LearningDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public LearningDbContext(DbContextOptions<LearningDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<SkillStage> SkillStages => Set<SkillStage>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<UserSkillProgress> UserSkillProgressRecords => Set<UserSkillProgress>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonVersion> LessonVersions => Set<LessonVersion>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<UserLessonProgress> UserLessonProgressRecords => Set<UserLessonProgress>();
    public DbSet<UserExerciseAttempt> UserExerciseAttempts => Set<UserExerciseAttempt>();
    public DbSet<ExerciseTypePrompt> ExerciseTypePrompts => Set<ExerciseTypePrompt>();
    public DbSet<ReferenceMaterial> ReferenceMaterials => Set<ReferenceMaterial>();
    public DbSet<DailyQuote> DailyQuotes => Set<DailyQuote>();
    public DbSet<Technique> Techniques => Set<Technique>();
    public DbSet<TechniqueSkill> TechniqueSkills => Set<TechniqueSkill>();
    public DbSet<TechniqueCoach> TechniqueCoaches => Set<TechniqueCoach>();
    public DbSet<UserTechniqueProgress> UserTechniqueProgressRecords => Set<UserTechniqueProgress>();
    public DbSet<UserReplica> UserReplicas => Set<UserReplica>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LearningDbContext).Assembly);

        // Phase 40.10. Convenience, not security — the boundary is the RLS policy created by the
        // AddOrganizationId migration (docs/TENANCY/TENANCY.md 1.4). Every tenant-scoped entity is
        // listed explicitly: EF query filters are NOT inherited through navigations, so a filter on
        // Skill says nothing about Topic, Lesson or Exercise even though the read path composes
        // Skill -> Topic -> Lesson -> Exercise. LearningDbContextQueryFilterTests fails the build if
        // an entity ever grows an OrganizationId without appearing here.

        // Tenant data: exactly one owning organization per row.
        modelBuilder.Entity<UserSkillProgress>()
            .HasQueryFilter(record => _tenantContext.IsPlatformWide || record.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<UserLessonProgress>()
            .HasQueryFilter(record => _tenantContext.IsPlatformWide || record.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<UserExerciseAttempt>()
            .HasQueryFilter(record => _tenantContext.IsPlatformWide || record.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<UserTechniqueProgress>()
            .HasQueryFilter(record => _tenantContext.IsPlatformWide || record.OrganizationId == _tenantContext.OrganizationId);

        // Content: null means the global library shared by every organization, so the comparison is
        // "mine or global", never plain equality (docs/TENANCY/CONTENT_MODEL.md).
        modelBuilder.Entity<Skill>()
            .HasQueryFilter(skill => _tenantContext.IsPlatformWide || skill.OrganizationId == null || skill.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<Topic>()
            .HasQueryFilter(topic => _tenantContext.IsPlatformWide || topic.OrganizationId == null || topic.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<Lesson>()
            .HasQueryFilter(lesson => _tenantContext.IsPlatformWide || lesson.OrganizationId == null || lesson.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<Exercise>()
            .HasQueryFilter(exercise => _tenantContext.IsPlatformWide || exercise.OrganizationId == null || exercise.OrganizationId == _tenantContext.OrganizationId);
        // Phase 40.15. A lesson version inherits the visibility of the lesson it snapshots, but the
        // filter has to be stated here anyway: EF query filters are not inherited through the
        // LessonVersion -> Lesson navigation.
        modelBuilder.Entity<LessonVersion>()
            .HasQueryFilter(version => _tenantContext.IsPlatformWide || version.OrganizationId == null || version.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<Technique>()
            .HasQueryFilter(technique => _tenantContext.IsPlatformWide || technique.OrganizationId == null || technique.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<ReferenceMaterial>()
            .HasQueryFilter(material => _tenantContext.IsPlatformWide || material.OrganizationId == null || material.OrganizationId == _tenantContext.OrganizationId);
    }
}
