using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Features.Techniques.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Tests.Unit;

/// <summary>
/// Phase 40.10. Fast, provider-independent guards for the three tenancy mistakes that are easiest
/// to make in this service and hardest to notice: forgetting a query filter on one entity of a
/// navigation chain, writing plain equality where the content library needs
/// "mine or global", and treating an unset tenant as "everything".
///
/// <para>
/// The real isolation boundary is Postgres row-level security and is covered by
/// <c>Integration/LearningTenantIsolationIntegrationTests</c> against a real database. These tests
/// are the cheap tripwire that runs on every build.
/// </para>
/// </summary>
[TestFixture]
public class LearningTenancyModelTests
{
    /// <summary>
    /// The trap docs/TENANCY/TENANCY.md §1.4 names explicitly: EF query filters are NOT inherited
    /// through navigations, so a filter on Skill says nothing about Topic, Lesson or Exercise even
    /// though every read path composes Skill -> Topic -> Lesson -> Exercise. Rather than listing
    /// the entities again here (which would just be the same omission twice), this walks the model
    /// and demands a filter from anything that carries an OrganizationId.
    /// </summary>
    [Test]
    public void Every_entity_with_an_organization_id_has_its_own_query_filter()
    {
        using var databaseContext = LearningDbContextFactory.CreateInMemory();

        var entitiesMissingAFilter = databaseContext.Model.GetEntityTypes()
            .Where(entityType => entityType.FindProperty(nameof(ITenantScoped.OrganizationId)) is not null)
            .Where(entityType => entityType.GetQueryFilter() is null)
            .Select(entityType => entityType.ClrType.Name)
            .OrderBy(name => name)
            .ToList();

        // OutboxMessage is the one deliberate exception: it carries the organization as payload for
        // the relay to republish, is read only by the platform-global outbox relay, and has no RLS
        // policy (docs/TENANCY/TENANCY.md §1.7).
        entitiesMissingAFilter.Should().BeEquivalentTo(["OutboxMessage"]);
    }

    /// <summary>
    /// The 2026-08-16 role split widened every filter so validated platform staff read across
    /// organizations. A filter that forgot the branch fails silently in production — Sellevate
    /// staff open the screen, see nothing, and everybody concludes the data is missing rather than
    /// filtered — so a missing branch fails the build here instead.
    /// </summary>
    [Test]
    public void Every_query_filter_admits_platform_staff()
    {
        using var databaseContext = LearningDbContextFactory.CreateInMemory();

        var filtersMissingThePlatformBranch = databaseContext.Model.GetEntityTypes()
            .Where(entityType => entityType.GetQueryFilter() is not null)
            .Where(entityType => !entityType.GetQueryFilter()!.ToString()
                .Contains(nameof(ITenantContext.IsPlatformWide), StringComparison.Ordinal))
            .Select(entityType => entityType.ClrType.Name)
            .OrderBy(name => name)
            .ToList();

        filtersMissingThePlatformBranch.Should().BeEmpty();
    }

    [Test]
    public void Progress_tables_are_tenant_scoped_and_content_tables_are_not()
    {
        typeof(UserSkillProgress).Should().BeAssignableTo<ITenantScoped>();
        typeof(UserLessonProgress).Should().BeAssignableTo<ITenantScoped>();
        typeof(UserExerciseAttempt).Should().BeAssignableTo<ITenantScoped>();
        typeof(UserTechniqueProgress).Should().BeAssignableTo<ITenantScoped>();

        // Content is nullable-owner by design — NULL is the global library, so it cannot implement
        // an interface whose OrganizationId is a non-nullable Guid.
        typeof(Skill).Should().NotBeAssignableTo<ITenantScoped>();
        typeof(Topic).Should().NotBeAssignableTo<ITenantScoped>();
        typeof(Lesson).Should().NotBeAssignableTo<ITenantScoped>();
        typeof(Exercise).Should().NotBeAssignableTo<ITenantScoped>();
        typeof(Technique).Should().NotBeAssignableTo<ITenantScoped>();
    }

    /// <summary>
    /// Content must be readable by every organization while it is global. Plain equality here would
    /// hand each new customer an empty skill tree on day one.
    /// </summary>
    [Test]
    public async Task Global_content_is_visible_to_every_organization()
    {
        var databaseName = Guid.NewGuid().ToString();
        var organizationAId = Guid.NewGuid();
        var organizationBId = Guid.NewGuid();

        await using (var seedContext = LearningDbContextFactory.CreateInMemory(databaseName, organizationAId))
        {
            seedContext.Skills.Add(new Skill { Id = Guid.NewGuid(), OrganizationId = null, IconicName = "global", Title = "Global" });
            seedContext.Skills.Add(new Skill { Id = Guid.NewGuid(), OrganizationId = organizationAId, IconicName = "owned-by-a", Title = "A" });
            seedContext.Skills.Add(new Skill { Id = Guid.NewGuid(), OrganizationId = organizationBId, IconicName = "owned-by-b", Title = "B" });
            await seedContext.SaveChangesAsync();
        }

        await using var organizationAContext = LearningDbContextFactory.CreateInMemory(databaseName, organizationAId);
        var visibleToA = await organizationAContext.Skills.Select(skill => skill.IconicName).ToListAsync();

        await using var organizationBContext = LearningDbContextFactory.CreateInMemory(databaseName, organizationBId);
        var visibleToB = await organizationBContext.Skills.Select(skill => skill.IconicName).ToListAsync();

        visibleToA.Should().BeEquivalentTo("global", "owned-by-a");
        visibleToB.Should().BeEquivalentTo("global", "owned-by-b");
    }

    [Test]
    public async Task One_organizations_progress_is_invisible_to_another()
    {
        var databaseName = Guid.NewGuid().ToString();
        var organizationAId = Guid.NewGuid();
        var organizationBId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var sharedUserId = Guid.NewGuid();

        await using (var organizationAContext = LearningDbContextFactory.CreateInMemory(databaseName, organizationAId))
        {
            organizationAContext.UserLessonProgressRecords.Add(new UserLessonProgress
            {
                Id = Guid.NewGuid(),
                UserId = sharedUserId,
                LessonId = lessonId,
                Status = "completed",
                BestScore = 100,
            });
            await organizationAContext.SaveChangesAsync();
        }

        await using var organizationBContext = LearningDbContextFactory.CreateInMemory(databaseName, organizationBId);
        var visibleToB = await organizationBContext.UserLessonProgressRecords.ToListAsync();

        visibleToB.Should().BeEmpty();
    }

    /// <summary>
    /// Roadmap 40.14: "an unset tenant is an exception, never a licence". Reads must fail closed and
    /// writes must fail loudly — the failure mode to avoid is an empty context silently meaning
    /// "all organizations".
    /// </summary>
    [Test]
    public async Task An_unset_tenant_sees_no_progress_and_cannot_write_any()
    {
        var databaseName = Guid.NewGuid().ToString();
        var organizationId = Guid.NewGuid();

        await using (var tenantContextualContext = LearningDbContextFactory.CreateInMemory(databaseName, organizationId))
        {
            tenantContextualContext.UserExerciseAttempts.Add(new UserExerciseAttempt
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                ExerciseId = Guid.NewGuid(),
                AttemptedAt = DateTime.UtcNow,
            });
            await tenantContextualContext.SaveChangesAsync();
        }

        var emptyTenantContext = new TenantContext();
        var options = new DbContextOptionsBuilder<LearningDbContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(new TenantSaveChangesInterceptor(emptyTenantContext))
            .Options;

        await using var unscopedContext = new LearningDbContext(options, emptyTenantContext);

        var visibleAttempts = await unscopedContext.UserExerciseAttempts.ToListAsync();
        visibleAttempts.Should().BeEmpty();

        unscopedContext.UserExerciseAttempts.Add(new UserExerciseAttempt
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ExerciseId = Guid.NewGuid(),
            AttemptedAt = DateTime.UtcNow,
        });

        var act = async () => await unscopedContext.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Organization context is not set.");
    }

    [Test]
    public async Task Writing_another_organizations_progress_row_is_refused()
    {
        var organizationId = Guid.NewGuid();
        await using var databaseContext = LearningDbContextFactory.CreateInMemory(organizationId);

        databaseContext.UserSkillProgressRecords.Add(new UserSkillProgress
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SkillId = Guid.NewGuid(),
        });

        var act = async () => await databaseContext.SaveChangesAsync();

        await act.Should().ThrowAsync<CrossTenantWriteException>();
    }
}
