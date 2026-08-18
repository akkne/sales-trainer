using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Gamification.Features.Achievements.Models;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.League.Models;
using Sellevate.Gamification.Identity;

namespace Sellevate.Gamification.Tests.Unit;

/// <summary>
/// The model-walking tripwires 40.13 shipped gamification-service without — <c>GamificationDbContext</c>
/// already names this fixture in a comment, so its absence was a gap rather than a decision. They
/// answer the two questions a reviewer cannot answer by reading the context file top to bottom: has
/// every tenant-owned entity got its own filter, and does every filter still admit platform staff.
///
/// <para>
/// Both failures are invisible at runtime in opposite directions. A missing filter leaks one
/// customer's league standings into another's; a filter missing the platform branch shows Sellevate
/// staff an empty screen they will read as "no data". Neither throws, so neither surfaces without a
/// test that walks the model.
/// </para>
/// </summary>
[TestFixture]
public class GamificationTenancyModelTests
{
    /// <summary>
    /// EF does not inherit a query filter through a navigation, so listing entities once in
    /// <c>OnModelCreating</c> is not enough — each needs its own. This walks the built model rather
    /// than the source, so an entity added later is covered without anyone remembering to.
    ///
    /// <para>
    /// <c>OutboxMessage</c> is the one deliberate exception, the same call learning (40.10) and company
    /// (40.12) made: it carries the organization as payload for the system-mode relay to republish, and
    /// has no RLS policy (docs/TENANCY/TENANCY.md §1.7).
    /// </para>
    /// </summary>
    [Test]
    public void Every_entity_with_an_organization_id_has_its_own_query_filter()
    {
        using var databaseContext = GamificationDbContextFactory.CreateInMemory();

        var entitiesMissingAFilter = databaseContext.Model.GetEntityTypes()
            .Where(entityType => entityType.FindProperty(nameof(ITenantScoped.OrganizationId)) is not null)
            .Where(entityType => entityType.GetQueryFilter() is null)
            .Select(entityType => entityType.ClrType.Name)
            .OrderBy(name => name)
            .ToList();

        entitiesMissingAFilter.Should().BeEquivalentTo(["OutboxMessage"]);
    }

    /// <summary>
    /// The 2026-08-16 role split widened every filter so validated platform staff read across
    /// organizations. A filter that forgot the branch fails silently in production, so a missing
    /// branch fails the build here instead.
    /// </summary>
    [Test]
    public void Every_query_filter_admits_platform_staff()
    {
        using var databaseContext = GamificationDbContextFactory.CreateInMemory();

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
    public void Per_user_records_and_leagues_are_tenant_scoped()
    {
        typeof(UserExperiencePointsRecord).Should().BeAssignableTo<ITenantScoped>();
        typeof(UserStreak).Should().BeAssignableTo<ITenantScoped>();
        typeof(UserAchievement).Should().BeAssignableTo<ITenantScoped>();
        typeof(UserLearningProgress).Should().BeAssignableTo<ITenantScoped>();
        typeof(League).Should().BeAssignableTo<ITenantScoped>();
        typeof(LeagueMembership).Should().BeAssignableTo<ITenantScoped>();
        typeof(LeagueSettings).Should().BeAssignableTo<ITenantScoped>();
    }

    /// <summary>
    /// The other half of the same claim: the catalogues and the installation-wide configuration
    /// carry no organization at all. That is what keeps "a row with no organization is invisible,
    /// not shared" true in this database — there is no nullable-owner content flavour here to make
    /// it ambiguous.
    /// </summary>
    [Test]
    public void Catalogues_and_installation_configuration_are_platform_global()
    {
        typeof(Achievement).Should().NotBeAssignableTo<ITenantScoped>();
        typeof(LeagueTier).Should().NotBeAssignableTo<ITenantScoped>();
        typeof(GamificationSettings).Should().NotBeAssignableTo<ITenantScoped>();
        typeof(StreakMilestone).Should().NotBeAssignableTo<ITenantScoped>();
        typeof(ExerciseTypeReward).Should().NotBeAssignableTo<ITenantScoped>();
        typeof(UserReplica).Should().NotBeAssignableTo<ITenantScoped>();
    }

    /// <summary>
    /// One organization's rows must not be visible to another through a plain context read. The
    /// in-memory provider has no row-level security, so this covers the query filter only — the RLS
    /// half is covered by the integration tests that need a real Postgres.
    /// </summary>
    [Test]
    public async Task One_organizations_streak_is_invisible_to_another()
    {
        var databaseName = Guid.NewGuid().ToString();
        var otherOrganizationId = Guid.Parse("0d9b8f8e-0000-4000-8000-0000000000ff");

        await using (var ownerContext = GamificationDbContextFactory.CreateInMemory(databaseName))
        {
            ownerContext.UserStreaks.Add(new UserStreak { UserId = Guid.NewGuid() });
            await ownerContext.SaveChangesAsync();
        }

        await using var otherContext = GamificationDbContextFactory.CreateInMemory(
            databaseName, otherOrganizationId);

        (await otherContext.UserStreaks.CountAsync()).Should().Be(0);
        (await otherContext.UserStreaks.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// And the widening actually widens: the same row the previous test hides from another
    /// organization is visible to platform staff, without them naming an organization at all.
    /// </summary>
    [Test]
    public async Task Platform_staff_see_every_organizations_rows()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using (var ownerContext = GamificationDbContextFactory.CreateInMemory(databaseName))
        {
            ownerContext.UserStreaks.Add(new UserStreak { UserId = Guid.NewGuid() });
            await ownerContext.SaveChangesAsync();
        }

        var platformContext = new TenantContext();
        platformContext.EnterPlatformMode();

        await using var staffContext = GamificationDbContextFactory.CreateInMemory(
            platformContext, databaseName);

        (await staffContext.UserStreaks.CountAsync()).Should().Be(1);
    }
}
