using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Features.Assignments.Models;
using Sellevate.Learning.Features.Content.Models;
using Sellevate.Learning.Features.ContentAdaptation.Models;
using Sellevate.Learning.Features.ContentGeneration.Models;
using Sellevate.Learning.Features.DailyQuotes.Models;
using Sellevate.Learning.Features.DialogReviews.Models;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Programs.Models;
using Sellevate.Learning.Features.Reference.Models;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Features.TeamInsights.Models;
using Sellevate.Learning.Features.Techniques.Models;
using Sellevate.Learning.Identity;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// learning-db's unit of work. It is the first database in the platform where <b>tenant data and the
/// global content library live side by side</b>, which is what makes its query filters worth reading
/// carefully: a progress row has exactly one owning organization, while a content row may have none
/// and then belongs to everybody.
///
/// <para>
/// Phase 40.10. The filters are <b>convenience, not security</b> — the boundary is the RLS policy
/// created by the <c>AddOrganizationId</c> migration (docs/TENANCY/TENANCY.md §1.4). Every
/// tenant-scoped entity is listed explicitly, because EF query filters are <b>not</b> inherited
/// through navigations: a filter on <c>Skill</c> says nothing about <c>Topic</c>, <c>Lesson</c> or
/// <c>Exercise</c> even though the read path composes Skill → Topic → Lesson → Exercise.
/// <c>LearningTenancyModelTests</c> fails the build if an entity ever grows an
/// <c>OrganizationId</c> without appearing here.
/// </para>
///
/// <para>
/// <b>Register this context with plain <c>AddDbContext</c> only.</b> EF Core's pooled-context helper
/// reuses an instance, and everything it closed over at construction time, across unrelated requests —
/// including the <see cref="ITenantContext"/> this class builds its filters from. The first
/// organization to touch a pooled instance would leak its filter onto every later caller. Enforced by
/// <c>scripts/tenancy-pool-lint.py</c>; see docs/CODESTYLE.md §6.
/// </para>
/// </summary>
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
    public DbSet<ContentAdaptationJob> ContentAdaptationJobs => Set<ContentAdaptationJob>();
    public DbSet<ContentAdaptationItem> ContentAdaptationItems => Set<ContentAdaptationItem>();
    public DbSet<TeamSkillGapDismissal> TeamSkillGapDismissals => Set<TeamSkillGapDismissal>();
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

        ApplyTenantScopedQueryFilters(modelBuilder);
        ApplyContentQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Strict tenant tables: exactly one owning organization per row, so the comparison is <b>plain
    /// equality</b> and never the "or global" branch the content tables use. A null owner on any of
    /// these would mean one customer's rows were readable by every other.
    ///
    /// <para>
    /// Phase 40.17. A curriculum is a decision one organization made about its own people, so there is
    /// no such thing as a global programme — the "or global" branch would hand every customer somebody
    /// else's training plan. Phase 40.21 gives an assignment the same shape, and Phase 40.22 a graded
    /// practice conversation: each happens inside one organization and there is no global one.
    /// </para>
    ///
    /// <para>
    /// Phase 40.25. A coaching note and a disputed score are things one organization's people said to
    /// each other about one organization's conversation. Phase 40.31's «Не предлагай нам это» is a
    /// decision one РОП made about their own team's panel — a null owner would silence one
    /// organization's suggestion for every other, the loudest possible version of the bug this whole
    /// phase exists to prevent.
    /// </para>
    ///
    /// <para>
    /// Phase 40.27 and 40.32. A pipeline run holds one customer's uploaded material and their extracted
    /// objections, script and compliance list; an adaptation batch holds proposals written out of their
    /// product, their tone and their banned claims. Plain equality — a null owner here would mean one
    /// customer's product deck, or their proposed rewrites, were readable and acceptable by every other.
    /// The adaptation <i>item</i> carries its own organization rather than inheriting the batch's,
    /// because query filters are not inherited through the Item → Job navigation and the RLS policy is
    /// per table. The lesson a run produces is content and is filtered by the content rule instead, but
    /// it is always owned rather than global.
    /// </para>
    ///
    /// <para>
    /// Phase 40.19. The substitution profile is tenant data even though it feeds content: there is no
    /// global profile, and a null owner would read as "every organization's product name".
    /// </para>
    /// </summary>
    private void ApplyTenantScopedQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserSkillProgress>()
            .HasQueryFilter(record => _tenantContext.IsPlatformWide || record.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<UserLessonProgress>()
            .HasQueryFilter(record => _tenantContext.IsPlatformWide || record.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<UserExerciseAttempt>()
            .HasQueryFilter(record => _tenantContext.IsPlatformWide || record.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<UserTechniqueProgress>()
            .HasQueryFilter(record => _tenantContext.IsPlatformWide || record.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<ProgramVersion>()
            .HasQueryFilter(version => _tenantContext.IsPlatformWide || version.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<ProgramItem>()
            .HasQueryFilter(item => _tenantContext.IsPlatformWide || item.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<ProgramEnrollment>()
            .HasQueryFilter(enrollment => _tenantContext.IsPlatformWide || enrollment.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<Assignment>()
            .HasQueryFilter(assignment => _tenantContext.IsPlatformWide || assignment.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<AssignmentProgress>()
            .HasQueryFilter(record => _tenantContext.IsPlatformWide || record.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<UserDialogScore>()
            .HasQueryFilter(score => _tenantContext.IsPlatformWide || score.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<DialogReviewNote>()
            .HasQueryFilter(note => _tenantContext.IsPlatformWide || note.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<ContentGenerationJob>()
            .HasQueryFilter(job => _tenantContext.IsPlatformWide || job.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<ContentAdaptationJob>()
            .HasQueryFilter(job => _tenantContext.IsPlatformWide || job.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<ContentAdaptationItem>()
            .HasQueryFilter(item => _tenantContext.IsPlatformWide || item.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<TeamSkillGapDismissal>()
            .HasQueryFilter(dismissal => _tenantContext.IsPlatformWide || dismissal.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<OrganizationProfileReplica>()
            .HasQueryFilter(replica => _tenantContext.IsPlatformWide || replica.OrganizationId == _tenantContext.OrganizationId);
    }

    /// <summary>
    /// Content tables: a null organization means the <b>global library shared by every organization</b>,
    /// so the comparison is "mine or global" and never plain equality
    /// (docs/TENANCY/CONTENT_MODEL.md).
    ///
    /// <para>
    /// Phase 40.15. A lesson version inherits the visibility of the lesson it snapshots, but the filter
    /// has to be stated here anyway: query filters are not inherited through the LessonVersion → Lesson
    /// navigation.
    /// </para>
    /// </summary>
    private void ApplyContentQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Skill>()
            .HasQueryFilter(skill => _tenantContext.IsPlatformWide || skill.OrganizationId == null || skill.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<Topic>()
            .HasQueryFilter(topic => _tenantContext.IsPlatformWide || topic.OrganizationId == null || topic.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<Lesson>()
            .HasQueryFilter(lesson => _tenantContext.IsPlatformWide || lesson.OrganizationId == null || lesson.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<Exercise>()
            .HasQueryFilter(exercise => _tenantContext.IsPlatformWide || exercise.OrganizationId == null || exercise.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<LessonVersion>()
            .HasQueryFilter(version => _tenantContext.IsPlatformWide || version.OrganizationId == null || version.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<Technique>()
            .HasQueryFilter(technique => _tenantContext.IsPlatformWide || technique.OrganizationId == null || technique.OrganizationId == _tenantContext.OrganizationId);
        modelBuilder.Entity<ReferenceMaterial>()
            .HasQueryFilter(material => _tenantContext.IsPlatformWide || material.OrganizationId == null || material.OrganizationId == _tenantContext.OrganizationId);
    }
}
