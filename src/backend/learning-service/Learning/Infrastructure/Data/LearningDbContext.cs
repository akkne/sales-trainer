using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Features.Assignments.Models;
using Sellevate.Learning.Features.Content.Models;
using Sellevate.Learning.Features.ContentGeneration.Models;
using Sellevate.Learning.Features.DailyQuotes.Models;
using Sellevate.Learning.Features.DialogReviews.Models;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Programs.Models;
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

    /// <summary>
    /// Phase 40.18. The tenant the query filters above are built from, exposed so that
    /// <c>ContentOverrideResolution</c> can decide whether to resolve overrides without every read
    /// service growing a second constructor parameter for a value this context already holds.
    /// Internal: it is a detail of how reads are composed, not part of the data model.
    /// </summary>
    internal ITenantContext TenantContext => _tenantContext;

    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<SkillStage> SkillStages => Set<SkillStage>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<UserSkillProgress> UserSkillProgressRecords => Set<UserSkillProgress>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonVersion> LessonVersions => Set<LessonVersion>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<UserLessonProgress> UserLessonProgressRecords => Set<UserLessonProgress>();
    public DbSet<UserExerciseAttempt> UserExerciseAttempts => Set<UserExerciseAttempt>();
    public DbSet<ProgramVersion> ProgramVersions => Set<ProgramVersion>();
    public DbSet<ProgramItem> ProgramItems => Set<ProgramItem>();
    public DbSet<ProgramEnrollment> ProgramEnrollments => Set<ProgramEnrollment>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentProgress> AssignmentProgressRecords => Set<AssignmentProgress>();
    public DbSet<UserDialogScore> UserDialogScores => Set<UserDialogScore>();
    public DbSet<DialogReviewNote> DialogReviewNotes => Set<DialogReviewNote>();
    public DbSet<ContentGenerationJob> ContentGenerationJobs => Set<ContentGenerationJob>();
    public DbSet<ExerciseTypePrompt> ExerciseTypePrompts => Set<ExerciseTypePrompt>();
    public DbSet<ReferenceMaterial> ReferenceMaterials => Set<ReferenceMaterial>();
    public DbSet<DailyQuote> DailyQuotes => Set<DailyQuote>();
    public DbSet<Technique> Techniques => Set<Technique>();
    public DbSet<TechniqueSkill> TechniqueSkills => Set<TechniqueSkill>();
    public DbSet<TechniqueCoach> TechniqueCoaches => Set<TechniqueCoach>();
    public DbSet<UserTechniqueProgress> UserTechniqueProgressRecords => Set<UserTechniqueProgress>();
    public DbSet<UserReplica> UserReplicas => Set<UserReplica>();
    public DbSet<OrganizationProfileReplica> OrganizationProfileReplicas => Set<OrganizationProfileReplica>();
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
        // Phase 40.17. A curriculum is a decision one organization made about its own people, so
        // there is no such thing as a global programme and the comparison is plain equality — the
        // "or global" branch below would hand every customer somebody else's training plan.
        modelBuilder.Entity<ProgramVersion>()
            .HasQueryFilter(version => _tenantContext.IsPlatformWide || version.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<ProgramItem>()
            .HasQueryFilter(item => _tenantContext.IsPlatformWide || item.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<ProgramEnrollment>()
            .HasQueryFilter(enrollment => _tenantContext.IsPlatformWide || enrollment.OrganizationId == _tenantContext.OrganizationId);
        // Phase 40.21. An assignment is a decision one organization made about its own people, the
        // same shape as a programme version: there is no global assignment, so the comparison is plain
        // equality and never the "or global" content branch below.
        modelBuilder.Entity<Assignment>()
            .HasQueryFilter(assignment => _tenantContext.IsPlatformWide || assignment.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<AssignmentProgress>()
            .HasQueryFilter(record => _tenantContext.IsPlatformWide || record.OrganizationId == _tenantContext.OrganizationId);
        // Phase 40.22. A graded practice conversation happens inside one organization; there is no
        // global one, so plain equality again.
        modelBuilder.Entity<UserDialogScore>()
            .HasQueryFilter(score => _tenantContext.IsPlatformWide || score.OrganizationId == _tenantContext.OrganizationId);
        // Phase 40.25. A coaching note and a disputed score are things one organization's people said
        // to each other about one organization's conversation. Plain equality, no global branch.
        modelBuilder.Entity<DialogReviewNote>()
            .HasQueryFilter(note => _tenantContext.IsPlatformWide || note.OrganizationId == _tenantContext.OrganizationId);
        // Phase 40.27. A pipeline run holds one customer's uploaded material and their extracted
        // objections, script and compliance list. Plain equality — a null owner here would mean one
        // customer's product deck was readable by every other. The lesson it produces is content and
        // is filtered by the content rule below, but it is always owned rather than global.
        modelBuilder.Entity<ContentGenerationJob>()
            .HasQueryFilter(job => _tenantContext.IsPlatformWide || job.OrganizationId == _tenantContext.OrganizationId);
        // Phase 40.19. The substitution profile is tenant data even though it feeds content: there
        // is no global profile, and a null owner would read as "every organization's product name".
        modelBuilder.Entity<OrganizationProfileReplica>()
            .HasQueryFilter(replica => _tenantContext.IsPlatformWide || replica.OrganizationId == _tenantContext.OrganizationId);

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
