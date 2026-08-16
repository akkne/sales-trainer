using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Eventing;
using Sellevate.Company.Features.Companies.FollowUpReminders;
using Sellevate.Company.Features.Companies.Models;
using Sellevate.Company.Features.Companies.Services.Implementation;
using Sellevate.Company.Infrastructure.Ai;
using Sellevate.Company.Tests.Helpers;
using CompanyEntity = Sellevate.Company.Features.Companies.Models.Company;

namespace Sellevate.Company.Tests.Unit;

/// <summary>
/// Phase 40.12. Fast, provider-independent guards for the mistakes that are easiest to make in this
/// service and hardest to notice: forgetting a query filter on one of the four entities that hang
/// off <c>Company</c>, dropping either half of the double scope, and letting a background job treat
/// an unset tenant as "everything".
///
/// <para>
/// The real isolation boundary is Postgres row-level security and is covered by
/// <c>Integration/CompanyTenantIsolationIntegrationTests</c> against a real database. These are the
/// cheap tripwires that run on every build.
/// </para>
/// </summary>
[TestFixture]
public class CompanyTenancyModelTests
{
    private static readonly Guid OrganizationAId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly Guid OrganizationBId = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000002");
    private static readonly Guid FirstUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// The trap docs/TENANCY/TENANCY.md §1.4 names explicitly: EF query filters are NOT inherited
    /// through navigations, so a filter on Company says nothing about the call logs, practice calls,
    /// contacts and personas hanging off it. Rather than listing the entities again here (which
    /// would just repeat whichever omission this is meant to catch), it walks the model and demands
    /// a filter from anything carrying an OrganizationId.
    /// </summary>
    [Test]
    public void Every_entity_with_an_organization_id_has_its_own_query_filter()
    {
        using var databaseContext = TestCompanyDatabaseFactory.CreateInMemory();

        var entitiesMissingAFilter = databaseContext.Model.GetEntityTypes()
            .Where(entityType => entityType.FindProperty(nameof(ITenantScoped.OrganizationId)) is not null)
            .Where(entityType => entityType.GetQueryFilter() is null)
            .Select(entityType => entityType.ClrType.Name)
            .OrderBy(name => name)
            .ToList();

        entitiesMissingAFilter.Should().BeEmpty();
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
        using var databaseContext = TestCompanyDatabaseFactory.CreateInMemory();

        var filtersMissingThePlatformBranch = databaseContext.Model.GetEntityTypes()
            .Where(entityType => entityType.GetQueryFilter() is not null)
            .Where(entityType => !entityType.GetQueryFilter()!.ToString()
                .Contains(nameof(ITenantContext.IsPlatformWide), StringComparison.Ordinal))
            .Select(entityType => entityType.ClrType.Name)
            .OrderBy(name => name)
            .ToList();

        filtersMissingThePlatformBranch.Should().BeEmpty();
    }

    /// <summary>
    /// company-db has no global content library — there is no table here whose rows are shared
    /// between organizations — so every entity is tenant-scoped, strictly.
    /// </summary>
    [Test]
    public void Every_company_entity_is_tenant_scoped()
    {
        typeof(CompanyEntity).Should().BeAssignableTo<ITenantScoped>();
        typeof(CallLogEntry).Should().BeAssignableTo<ITenantScoped>();
        typeof(PracticeCall).Should().BeAssignableTo<ITenantScoped>();
        typeof(CompanyContact).Should().BeAssignableTo<ITenantScoped>();
        typeof(CompanyPersona).Should().BeAssignableTo<ITenantScoped>();

        using var databaseContext = TestCompanyDatabaseFactory.CreateInMemory();
        var entitiesWithoutAnOrganization = databaseContext.Model.GetEntityTypes()
            .Where(entityType => entityType.FindProperty(nameof(ITenantScoped.OrganizationId)) is null)
            .Select(entityType => entityType.ClrType.Name)
            .ToList();

        entitiesWithoutAnOrganization.Should().BeEmpty();
    }

    /// <summary>First half of the double scope: organization A cannot see organization B's data.</summary>
    [Test]
    public async Task A_company_written_by_one_organization_is_invisible_to_another()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using (var organizationA = TestCompanyDatabaseFactory.CreateInMemory(
            TestCompanyDatabaseFactory.BuildTenantContext(OrganizationAId), databaseName))
        {
            await TestCompanyDatabaseFactory.SeedCompanyAsync(organizationA, FirstUserId, "Org A prospect");
        }

        await using var organizationB = TestCompanyDatabaseFactory.CreateInMemory(
            TestCompanyDatabaseFactory.BuildTenantContext(OrganizationBId), databaseName);

        (await organizationB.Companies.CountAsync()).Should().Be(0);
        (await organizationB.Companies.IgnoreQueryFilters().CountAsync()).Should().Be(1,
            "the row exists — it is the filter, not an empty database, that hides it");
    }

    /// <summary>
    /// Second half, and the one a single-tenant mindset misses: inside ONE organization, a
    /// salesperson must not see a colleague's pipeline. The organization filter passes here by
    /// design; only the explicit user predicate in CompanyService stops the leak.
    /// </summary>
    [Test]
    public async Task A_user_does_not_see_a_colleagues_companies_inside_the_same_organization()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var databaseContext = TestCompanyDatabaseFactory.CreateInMemory(
            TestCompanyDatabaseFactory.BuildTenantContext(OrganizationAId), databaseName);

        await TestCompanyDatabaseFactory.SeedCompanyAsync(databaseContext, FirstUserId, "Mine");
        await TestCompanyDatabaseFactory.SeedCompanyAsync(databaseContext, SecondUserId, "My colleague's");

        var companyService = CreateCompanyService(databaseContext);

        var firstUsersCompanies = await companyService.ListCompaniesAsync(FirstUserId, search: null);
        var secondUsersCompanies = await companyService.ListCompaniesAsync(SecondUserId, search: null);

        firstUsersCompanies.Select(company => company.Name).Should().BeEquivalentTo(["Mine"]);
        secondUsersCompanies.Select(company => company.Name).Should().BeEquivalentTo(["My colleague's"]);

        // Same organization, so the tenant filter alone would have shown both.
        (await databaseContext.Companies.CountAsync()).Should().Be(2);
    }

    /// <summary>
    /// Sub-resources carry the user half themselves rather than inheriting it from the ownership
    /// check on the parent. Reaching a colleague's call log through their company id must fail on
    /// the parent check AND leave nothing readable if it somehow did not.
    /// </summary>
    [Test]
    public async Task A_user_cannot_read_a_colleagues_call_log_through_their_company_id()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var databaseContext = TestCompanyDatabaseFactory.CreateInMemory(
            TestCompanyDatabaseFactory.BuildTenantContext(OrganizationAId), databaseName);

        var colleaguesCompany = await TestCompanyDatabaseFactory.SeedCompanyAsync(
            databaseContext, SecondUserId, "My colleague's");

        var companyService = CreateCompanyService(databaseContext);
        await companyService.CreateCallLogEntryAsync(
            SecondUserId,
            colleaguesCompany.Id,
            new CreateCallLogEntryRequestDto("Their contact", "Their subject", "Their outcome", DateTime.UtcNow, null));

        var seenByColleague = await companyService.ListCallLogEntriesAsync(SecondUserId, colleaguesCompany.Id);
        var seenByOtherUser = await companyService.ListCallLogEntriesAsync(FirstUserId, colleaguesCompany.Id);

        seenByColleague.Should().HaveCount(1);
        seenByOtherUser.Should().BeNull("an id owned by somebody else is indistinguishable from an id that does not exist");
    }

    /// <summary>An unset tenant reads nothing, rather than everything.</summary>
    [Test]
    public async Task An_unset_tenant_reads_no_companies()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using (var organizationA = TestCompanyDatabaseFactory.CreateInMemory(
            TestCompanyDatabaseFactory.BuildTenantContext(OrganizationAId), databaseName))
        {
            await TestCompanyDatabaseFactory.SeedCompanyAsync(organizationA, FirstUserId, "Org A prospect");
        }

        await using var noTenant = TestCompanyDatabaseFactory.CreateInMemory(new TenantContext(), databaseName);

        (await noTenant.Companies.CountAsync()).Should().Be(0);
    }

    /// <summary>Writing a foreign organization is refused by the write guard, not silently accepted.</summary>
    [Test]
    public async Task Writing_a_foreign_organization_id_raises()
    {
        await using var databaseContext = TestCompanyDatabaseFactory.CreateInMemory(
            TestCompanyDatabaseFactory.BuildTenantContext(OrganizationAId));

        databaseContext.Companies.Add(new CompanyEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = OrganizationBId,
            UserId = FirstUserId,
            Name = "Planted",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var write = async () => await databaseContext.SaveChangesAsync();

        await write.Should().ThrowAsync<CrossTenantWriteException>();
    }

    /// <summary>
    /// The roadmap's explicit requirement for background jobs: an unset tenant must raise, never
    /// mean "everything". Before 40.12 this service scanned every organization's companies.
    /// </summary>
    [Test]
    public async Task The_follow_up_reminder_service_raises_on_an_unset_tenant()
    {
        var tenantContext = new TenantContext();
        await using var databaseContext = TestCompanyDatabaseFactory.CreateInMemory(tenantContext);
        var reminderService = CreateReminderService(databaseContext, tenantContext);

        var poll = async () => await reminderService.ProcessDueFollowUpsAsync();

        (await poll.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*requires an organization*");
    }

    /// <summary>System mode is not the same as "no tenant", and it is refused just as firmly.</summary>
    [Test]
    public async Task The_follow_up_reminder_service_refuses_to_run_in_system_mode()
    {
        var tenantContext = new TenantContext();
        tenantContext.EnterSystemMode();
        await using var databaseContext = TestCompanyDatabaseFactory.CreateInMemory(tenantContext);
        var reminderService = CreateReminderService(databaseContext, tenantContext);

        var poll = async () => await reminderService.ProcessDueFollowUpsAsync();

        (await poll.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*system mode*");
    }

    /// <summary>
    /// One tick, one organization: the poll must not claim or publish another organization's due
    /// follow-up even when both are due at the same moment.
    /// </summary>
    [Test]
    public async Task The_follow_up_reminder_service_never_processes_across_organizations()
    {
        var databaseName = Guid.NewGuid().ToString();
        var dueAt = DateTime.UtcNow.AddMinutes(-5);

        await using (var organizationB = TestCompanyDatabaseFactory.CreateInMemory(
            TestCompanyDatabaseFactory.BuildTenantContext(OrganizationBId), databaseName))
        {
            await TestCompanyDatabaseFactory.SeedCompanyAsync(
                organizationB, SecondUserId, "Org B prospect", nextActionAt: dueAt);
        }

        var tenantContext = TestCompanyDatabaseFactory.BuildTenantContext(OrganizationAId);
        await using var organizationA = TestCompanyDatabaseFactory.CreateInMemory(tenantContext, databaseName);
        await TestCompanyDatabaseFactory.SeedCompanyAsync(
            organizationA, FirstUserId, "Org A prospect", nextActionAt: dueAt);

        var eventPublisher = Substitute.For<IEventPublisher>();
        var reminderService = CreateReminderService(organizationA, tenantContext, eventPublisher);

        var publishedCount = await reminderService.ProcessDueFollowUpsAsync();

        publishedCount.Should().Be(1, "only organization A's follow-up is in scope for this tick");
        await eventPublisher.Received(1).PublishAsync(
            Topics.CompanyFollowUpDue,
            FirstUserId.ToString(),
            Topics.CompanyFollowUpDue,
            Arg.Is<CompanyFollowUpDueEvent>(payload => payload.CompanyName == "Org A prospect"),
            1,
            OrganizationAId,
            Arg.Any<CancellationToken>());

        // And organization B's row is untouched: still unnotified, still due, waiting for its own tick.
        var organizationBRow = await organizationA.Companies
            .IgnoreQueryFilters()
            .SingleAsync(company => company.OrganizationId == OrganizationBId);
        organizationBRow.FollowUpNotifiedAt.Should().BeNull();
    }

    /// <summary>The organization travels in the envelope, which is where a consumer looks for it.</summary>
    [Test]
    public async Task The_due_follow_up_event_carries_the_organization_in_the_envelope()
    {
        var tenantContext = TestCompanyDatabaseFactory.BuildTenantContext(OrganizationAId);
        await using var databaseContext = TestCompanyDatabaseFactory.CreateInMemory(tenantContext);
        await TestCompanyDatabaseFactory.SeedCompanyAsync(
            databaseContext, FirstUserId, "Acme", nextActionAt: DateTime.UtcNow.AddMinutes(-5));

        var eventPublisher = Substitute.For<IEventPublisher>();
        var reminderService = CreateReminderService(databaseContext, tenantContext, eventPublisher);

        await reminderService.ProcessDueFollowUpsAsync();

        await eventPublisher.Received(1).PublishAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CompanyFollowUpDueEvent>(), Arg.Any<int>(),
            OrganizationAId,
            Arg.Any<CancellationToken>());
    }

    private static CompanyService CreateCompanyService(Infrastructure.Data.CompanyDbContext databaseContext) =>
        new(databaseContext,
            Substitute.For<IBriefingAiClient>(),
            Substitute.For<IParseLogAiClient>(),
            Substitute.For<IPersonaAiClient>(),
            Substitute.For<IReadinessAiClient>());

    private static FollowUpReminderService CreateReminderService(
        Infrastructure.Data.CompanyDbContext databaseContext,
        ITenantContext tenantContext,
        IEventPublisher? eventPublisher = null) =>
        new(databaseContext,
            tenantContext,
            eventPublisher ?? Substitute.For<IEventPublisher>(),
            Options.Create(new FollowUpReminderOptions { BatchSize = 100 }),
            NullLogger<FollowUpReminderService>.Instance);
}
