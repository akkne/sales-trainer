using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using NSubstitute;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Eventing;
using Sellevate.Company.Features.Companies.FollowUpReminders;
using Sellevate.Company.Features.Companies.Services.Implementation;
using Sellevate.Company.Infrastructure.Ai;
using Sellevate.Company.Infrastructure.Data;

namespace Sellevate.Company.Tests.Integration;

/// <summary>
/// Phase 40.12 — the point of the whole block, and it has two halves rather than one.
///
/// <para>
/// <b>Organization:</b> two organizations, and neither can reach the other's CRM through any door.
/// <b>User:</b> two users inside the SAME organization, and neither can reach the other's pipeline.
/// Getting either wrong is a bug in a different direction — the first leaks between customers, the
/// second hands a salesperson a colleague's private prospect list — so both are asserted here, and
/// the second is asserted through the service layer, because the database is deliberately not the
/// thing that enforces it.
/// </para>
///
/// <para>
/// Verified against a real Postgres, because the doors that matter are the ones the EF query filter
/// does not guard — a navigation property, raw SQL, and <c>ExecuteUpdate</c> / <c>ExecuteDelete</c>,
/// none of which the in-memory provider can model. The schema is built by running company-service's
/// own EF migrations, not by a hand-written CREATE TABLE script: what is under test is the
/// row-level security migration <c>20260815203733_AddOrganizationId</c> actually emits, not a
/// re-statement of it.
/// </para>
///
/// <para>
/// Runs against the same local Docker Postgres <c>scripts/dev-infra.sh</c> starts
/// (<c>localhost:5433</c> by default — see <see cref="LocalCompanyPostgresTestSettings"/>), inside
/// its own throwaway database and a throwaway NOBYPASSRLS login role, both dropped and recreated at
/// the start of the run and dropped again at the end. It never touches the real <c>company</c>
/// database. Per CLAUDE.md and docs/DONT_FORGET.md it must never hang or fail the build when no
/// local Postgres is reachable: <see cref="OneTimeSetUpAsync"/> probes with a bounded timeout and
/// every test then skips in seconds via <c>Assert.Ignore</c>. See docs/TESTING/TENANCY.md for the
/// one command that runs it and what to look for.
/// </para>
/// </summary>
[TestFixture]
[Category("Integration")]
public class CompanyTenantIsolationIntegrationTests
{
    private bool _isPostgresReachable;

    private static readonly Guid OrganizationAId = new("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid OrganizationBId = new("b0000000-0000-4000-8000-000000000002");

    // Two colleagues inside organization A — the pair the second half of the scope is about.
    private static readonly Guid FirstUserId = new("a0000000-0000-4000-8000-0000000000f1");
    private static readonly Guid ColleagueUserId = new("a0000000-0000-4000-8000-0000000000f2");
    private static readonly Guid OtherOrganizationUserId = new("b0000000-0000-4000-8000-0000000000f3");

    private static readonly Guid FirstUserCompanyId = new("a0000000-0000-4000-8000-00000000000a");
    private static readonly Guid ColleagueCompanyId = new("a0000000-0000-4000-8000-00000000000b");
    private static readonly Guid OtherOrganizationCompanyId = new("b0000000-0000-4000-8000-00000000000c");

    private static readonly Guid FirstUserCallLogId = new("a0000000-0000-4000-8000-0000000000e1");
    private static readonly Guid ColleagueCallLogId = new("a0000000-0000-4000-8000-0000000000e2");
    private static readonly Guid OtherOrganizationCallLogId = new("b0000000-0000-4000-8000-0000000000e3");

    private static readonly Guid FirstUserContactId = new("a0000000-0000-4000-8000-0000000000d1");
    private static readonly Guid OtherOrganizationContactId = new("b0000000-0000-4000-8000-0000000000d2");

    private static readonly Guid FirstUserPersonaId = new("a0000000-0000-4000-8000-0000000000c1");
    private static readonly Guid OtherOrganizationPersonaId = new("b0000000-0000-4000-8000-0000000000c2");

    private static readonly Guid FirstUserPracticeCallId = new("a0000000-0000-4000-8000-0000000000b1");
    private static readonly Guid OtherOrganizationPracticeCallId = new("b0000000-0000-4000-8000-0000000000b2");

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _isPostgresReachable = await LocalCompanyPostgresTestSettings.IsReachableAsync();
        if (!_isPostgresReachable)
        {
            return;
        }

        await using (var maintenanceConnection = new NpgsqlConnection(LocalCompanyPostgresTestSettings.AdminConnectionString()))
        {
            await maintenanceConnection.OpenAsync();
            await ExecuteAsync(maintenanceConnection, $"""
                SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                WHERE datname = '{LocalCompanyPostgresTestSettings.TestDatabaseName}' AND pid <> pg_backend_pid();
                """);
            await ExecuteAsync(maintenanceConnection, $"DROP DATABASE IF EXISTS {LocalCompanyPostgresTestSettings.TestDatabaseName};");
            await ExecuteAsync(maintenanceConnection, $"DROP ROLE IF EXISTS {LocalCompanyPostgresTestSettings.ApplicationRoleName};");
            await ExecuteAsync(maintenanceConnection, $"CREATE DATABASE {LocalCompanyPostgresTestSettings.TestDatabaseName};");
            // NOBYPASSRLS is the whole point: FORCE ROW LEVEL SECURITY still lets a superuser (and,
            // without FORCE, the table owner) read everything, so isolation tested as the admin
            // role would pass no matter what the policies said.
            await ExecuteAsync(maintenanceConnection, $"""
                CREATE ROLE {LocalCompanyPostgresTestSettings.ApplicationRoleName}
                    WITH LOGIN PASSWORD '{LocalCompanyPostgresTestSettings.ApplicationRolePassword}'
                    NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
                """);
            await ExecuteAsync(maintenanceConnection, $"""
                GRANT CONNECT ON DATABASE {LocalCompanyPostgresTestSettings.TestDatabaseName}
                    TO {LocalCompanyPostgresTestSettings.ApplicationRoleName};
                """);
        }

        // The real migrations, run as the owner — exactly how production gets its schema and its
        // RLS policies. Nothing in this fixture writes DDL of its own.
        await using (var migrationContext = CreateAdminContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        await using var testDatabaseConnection = new NpgsqlConnection(
            LocalCompanyPostgresTestSettings.AdminConnectionString(LocalCompanyPostgresTestSettings.TestDatabaseName));
        await testDatabaseConnection.OpenAsync();

        await ExecuteAsync(testDatabaseConnection, $"""
            GRANT USAGE ON SCHEMA public TO {LocalCompanyPostgresTestSettings.ApplicationRoleName};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public
                TO {LocalCompanyPostgresTestSettings.ApplicationRoleName};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public
                TO {LocalCompanyPostgresTestSettings.ApplicationRoleName};
            """);

        await SeedAsync(testDatabaseConnection);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        if (!_isPostgresReachable)
        {
            return;
        }

        try
        {
            await using var maintenanceConnection = new NpgsqlConnection(LocalCompanyPostgresTestSettings.AdminConnectionString());
            await maintenanceConnection.OpenAsync();
            await ExecuteAsync(maintenanceConnection, $"""
                SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                WHERE datname = '{LocalCompanyPostgresTestSettings.TestDatabaseName}' AND pid <> pg_backend_pid();
                """);
            await ExecuteAsync(maintenanceConnection, $"DROP DATABASE IF EXISTS {LocalCompanyPostgresTestSettings.TestDatabaseName};");
            await ExecuteAsync(maintenanceConnection, $"DROP ROLE IF EXISTS {LocalCompanyPostgresTestSettings.ApplicationRoleName};");
        }
        catch
        {
            // Best-effort cleanup of a throwaway fixture — never fail the run over teardown.
        }
    }

    // ---------------------------------------------------------------------------------------
    // Half one: the organization boundary.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// The trap docs/TENANCY/TENANCY.md §1.4 calls out by name: query filters are not inherited
    /// through navigations. Loading companies with their whole sub-tree eagerly must exclude the
    /// other organization at every level, not just at the root the query started from.
    /// </summary>
    [Test]
    public async Task A_read_through_the_navigation_chain_never_crosses_the_organization_boundary()
    {
        SkipIfPostgresIsNotReachable();

        await using var context = CreateApplicationContext(OrganizationAId);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var companies = await context.Companies
            .Include(company => company.CallLogEntries)
            .Include(company => company.PracticeCalls)
            .Include(company => company.Contacts)
            .Include(company => company.Personas)
            .ToListAsync();

        companies.Select(company => company.Id)
            .Should().BeEquivalentTo(new[] { FirstUserCompanyId, ColleagueCompanyId });

        // Every hop of the chain, not only the entity the query started from.
        companies.SelectMany(company => company.CallLogEntries).Select(entry => entry.Id)
            .Should().NotContain(OtherOrganizationCallLogId);
        companies.SelectMany(company => company.PracticeCalls).Select(practiceCall => practiceCall.Id)
            .Should().NotContain(OtherOrganizationPracticeCallId);
        companies.SelectMany(company => company.Contacts).Select(contact => contact.Id)
            .Should().NotContain(OtherOrganizationContactId);
        companies.SelectMany(company => company.Personas).Select(persona => persona.Id)
            .Should().NotContain(OtherOrganizationPersonaId);

        await transaction.CommitAsync();
    }

    /// <summary>
    /// Raw SQL is the door the EF query filter does not guard at all. This is the layer
    /// docs/TENANCY/TENANCY.md §1.5 exists for.
    /// </summary>
    [Test]
    public async Task Raw_sql_only_returns_the_current_organizations_rows()
    {
        SkipIfPostgresIsNotReachable();

        await using var connection = new NpgsqlConnection(LocalCompanyPostgresTestSettings.ApplicationRoleConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetOrganizationAsync(connection, transaction, OrganizationAId);

        var visibleCompanyIds = await ReadGuidsAsync(connection, transaction, """SELECT "Id" FROM "Companies" ORDER BY "Id";""");
        var visibleCallLogIds = await ReadGuidsAsync(connection, transaction, """SELECT "Id" FROM "CallLogEntries" ORDER BY "Id";""");

        visibleCompanyIds.Should().BeEquivalentTo(new[] { FirstUserCompanyId, ColleagueCompanyId });
        visibleCallLogIds.Should().BeEquivalentTo(new[] { FirstUserCallLogId, ColleagueCallLogId });

        await transaction.CommitAsync();
    }

    /// <summary>
    /// company-db has no global content library, so — unlike learning-db and ai-db — an unset
    /// tenant sees literally nothing here. There is no table whose rows survive a missing tenant.
    /// </summary>
    [Test]
    public async Task Raw_sql_with_no_organization_setting_sees_nothing_at_all()
    {
        SkipIfPostgresIsNotReachable();

        await using var connection = new NpgsqlConnection(LocalCompanyPostgresTestSettings.ApplicationRoleConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT (SELECT count(*) FROM "Companies")
                 + (SELECT count(*) FROM "CallLogEntries")
                 + (SELECT count(*) FROM "PracticeCalls")
                 + (SELECT count(*) FROM "CompanyContacts")
                 + (SELECT count(*) FROM "CompanyPersonas");
            """;

        var visibleRowCount = (long)(await command.ExecuteScalarAsync())!;

        visibleRowCount.Should().Be(0, "an unset tenant must mean no data, never all data");
        await transaction.CommitAsync();
    }

    [Test]
    public async Task ExecuteUpdate_cannot_touch_another_organizations_company()
    {
        SkipIfPostgresIsNotReachable();

        await using (var context = CreateApplicationContext(OrganizationAId))
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            // IgnoreQueryFilters strips the EF-level guard on purpose: what is under test is what
            // survives when the convenience layer is gone.
            var updatedRowCount = await context.Companies
                .IgnoreQueryFilters()
                .Where(company => company.Id == OtherOrganizationCompanyId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(company => company.Name, "Hijacked"));

            updatedRowCount.Should().Be(0);
            await transaction.CommitAsync();
        }

        var survivingName = await ReadScalarAsAdminAsync<string>(
            """SELECT "Name" FROM "Companies" WHERE "Id" = @id;""",
            OtherOrganizationCompanyId);

        survivingName.Should().Be("Org B prospect", "organization B's company must be exactly as it was seeded");
    }

    [Test]
    public async Task ExecuteDelete_cannot_remove_another_organizations_call_log()
    {
        SkipIfPostgresIsNotReachable();

        await using (var context = CreateApplicationContext(OrganizationAId))
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            var deletedRowCount = await context.CallLogEntries
                .IgnoreQueryFilters()
                .Where(entry => entry.Id == OtherOrganizationCallLogId)
                .ExecuteDeleteAsync();

            deletedRowCount.Should().Be(0);
            await transaction.CommitAsync();
        }

        var survivingRowCount = await ReadScalarAsAdminAsync<long>(
            """SELECT count(*) FROM "CallLogEntries" WHERE "Id" = @id;""",
            OtherOrganizationCallLogId);

        survivingRowCount.Should().Be(1);
    }

    [Test]
    public async Task Inserting_a_company_for_another_organization_is_refused_by_postgres()
    {
        SkipIfPostgresIsNotReachable();

        await using var connection = new NpgsqlConnection(LocalCompanyPostgresTestSettings.ApplicationRoleConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetOrganizationAsync(connection, transaction, OrganizationAId);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO "Companies"
                ("Id", "OrganizationId", "UserId", "Name", "Description", "Status", "CreatedAt", "UpdatedAt")
            VALUES (@id, @organizationId, @userId, 'Planted', '', 'Lead', now(), now());
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("organizationId", OrganizationBId);
        command.Parameters.AddWithValue("userId", OtherOrganizationUserId);

        var act = async () => await command.ExecuteNonQueryAsync();

        var thrown = await act.Should().ThrowAsync<PostgresException>();
        thrown.Which.Message.Should().Contain("row-level security");
    }

    /// <summary>
    /// The failure mode a service method would hit if it forgot its <c>TenantTransactionScope</c>:
    /// <c>SET LOCAL</c> needs a transaction, so a bare SELECT sees nothing. Asserting it here keeps
    /// the reason for that scope rule from being forgotten — and in this service the symptom is
    /// especially quiet, because "no companies" looks exactly like a new user.
    /// </summary>
    [Test]
    public async Task A_read_without_a_transaction_sees_nothing_even_with_a_tenant_context()
    {
        SkipIfPostgresIsNotReachable();

        await using var context = CreateApplicationContext(OrganizationAId);

        var visibleCompanies = await context.Companies.ToListAsync();

        visibleCompanies.Should().BeEmpty(
            "SET LOCAL has no effect outside a transaction, so the RLS policy filters everything — "
            + "this is why every company-service read path opens a TenantTransactionScope");
    }

    // ---------------------------------------------------------------------------------------
    // Half two: the user boundary, inside one organization.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Both users are in organization A, so the RLS policy and the query filter admit both rows —
    /// deliberately. The only thing standing between a salesperson and a colleague's pipeline is
    /// the explicit user predicate in CompanyService, which is exactly why this is asserted through
    /// the service rather than through the DbContext.
    /// </summary>
    [Test]
    public async Task A_user_does_not_see_a_colleagues_companies_inside_the_same_organization()
    {
        SkipIfPostgresIsNotReachable();

        await using var context = CreateApplicationContext(OrganizationAId);
        var companyService = CreateCompanyService(context);

        var mine = await companyService.ListCompaniesAsync(FirstUserId, search: null);
        var theirs = await companyService.ListCompaniesAsync(ColleagueUserId, search: null);

        mine.Select(company => company.Id).Should().Equal(FirstUserCompanyId);
        theirs.Select(company => company.Id).Should().Equal(ColleagueCompanyId);
    }

    /// <summary>
    /// The sub-resource half of the same question: knowing a colleague's company id must not be
    /// enough to read its timeline, contacts or personas.
    /// </summary>
    [Test]
    public async Task A_colleagues_company_id_opens_none_of_its_sub_resources()
    {
        SkipIfPostgresIsNotReachable();

        await using var context = CreateApplicationContext(OrganizationAId);
        var companyService = CreateCompanyService(context);

        (await companyService.GetCompanyAsync(FirstUserId, ColleagueCompanyId)).Should().BeNull();
        (await companyService.ListCallLogEntriesAsync(FirstUserId, ColleagueCompanyId)).Should().BeNull();
        (await companyService.ListContactsAsync(FirstUserId, ColleagueCompanyId)).Should().BeNull();
        (await companyService.ListPersonasAsync(FirstUserId, ColleagueCompanyId)).Should().BeNull();
        (await companyService.ListPracticeCallsAsync(FirstUserId, ColleagueCompanyId)).Should().BeNull();

        // ...and the colleague still sees their own, so the assertions above are not just "empty
        // database" in disguise.
        (await companyService.GetCompanyAsync(ColleagueUserId, ColleagueCompanyId)).Should().NotBeNull();
        (await companyService.ListCallLogEntriesAsync(ColleagueUserId, ColleagueCompanyId)).Should().HaveCount(1);
    }

    /// <summary>
    /// A company id from ANOTHER organization is not even visible to the query filter, so it fails
    /// the same way an unknown id does — 404-shaped, never 403-shaped.
    /// </summary>
    [Test]
    public async Task A_company_id_from_another_organization_behaves_exactly_like_an_unknown_id()
    {
        SkipIfPostgresIsNotReachable();

        await using var context = CreateApplicationContext(OrganizationAId);
        var companyService = CreateCompanyService(context);

        (await companyService.GetCompanyAsync(FirstUserId, OtherOrganizationCompanyId)).Should().BeNull();
        (await companyService.GetCompanyAsync(FirstUserId, Guid.NewGuid())).Should().BeNull();
    }

    // ---------------------------------------------------------------------------------------
    // The background job.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Both organizations have a follow-up due at the same moment. A tick scoped to organization A
    /// must claim and publish exactly one of them — the pre-40.12 version claimed both.
    /// </summary>
    [Test]
    public async Task The_follow_up_poll_never_claims_another_organizations_due_company()
    {
        SkipIfPostgresIsNotReachable();

        var eventPublisher = Substitute.For<IEventPublisher>();

        await using (var context = CreateApplicationContext(OrganizationAId, out var tenantContext))
        {
            var reminderService = new FollowUpReminderService(
                context,
                tenantContext,
                eventPublisher,
                Options.Create(new FollowUpReminderOptions { BatchSize = 100 }),
                NullLogger<FollowUpReminderService>.Instance);

            var publishedCount = await reminderService.ProcessDueFollowUpsAsync();

            publishedCount.Should().Be(1, "only organization A's follow-up is in scope for this tick");
        }

        await eventPublisher.Received(1).PublishAsync(
            Topics.CompanyFollowUpDue,
            FirstUserId.ToString(),
            Topics.CompanyFollowUpDue,
            Arg.Is<CompanyFollowUpDueEvent>(payload => payload.CompanyId == FirstUserCompanyId),
            1,
            OrganizationAId,
            Arg.Any<CancellationToken>());

        // Organization B's row is untouched — still unnotified, still waiting for its own tick.
        var otherOrganizationNotifiedAt = await ReadScalarAsAdminAsync<object>(
            """SELECT COALESCE("FollowUpNotifiedAt"::text, 'null') FROM "Companies" WHERE "Id" = @id;""",
            OtherOrganizationCompanyId);

        otherOrganizationNotifiedAt.Should().Be("null");
    }

    /// <summary>
    /// The roadmap's explicit requirement for background jobs, asserted against a real database as
    /// well as in the unit tripwires: an unset tenant raises. It must never fall back to the
    /// pre-40.12 behaviour of scanning every organization.
    /// </summary>
    [Test]
    public async Task The_follow_up_poll_raises_on_an_unset_tenant_instead_of_scanning_everything()
    {
        SkipIfPostgresIsNotReachable();

        var tenantContext = new TenantContext();
        var options = new DbContextOptionsBuilder<CompanyDbContext>()
            .UseNpgsql(LocalCompanyPostgresTestSettings.ApplicationRoleConnectionString())
            .AddInterceptors(
                new TenantSaveChangesInterceptor(tenantContext),
                new TenantConnectionInterceptor(tenantContext))
            .Options;

        await using var context = new CompanyDbContext(options, tenantContext);
        var reminderService = new FollowUpReminderService(
            context,
            tenantContext,
            Substitute.For<IEventPublisher>(),
            Options.Create(new FollowUpReminderOptions { BatchSize = 100 }),
            NullLogger<FollowUpReminderService>.Instance);

        var poll = async () => await reminderService.ProcessDueFollowUpsAsync();

        (await poll.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*requires an organization*");
    }

    // ---------------------------------------------------------------------------------------

    private void SkipIfPostgresIsNotReachable()
    {
        if (!_isPostgresReachable)
        {
            Assert.Ignore(
                "Local Postgres is not reachable at localhost:5433 (or LOCAL_POSTGRES_PORT) — "
                + "start it with scripts/dev-infra.sh. Skipping the company-service tenant-isolation test.");
        }
    }

    private static CompanyService CreateCompanyService(CompanyDbContext databaseContext) =>
        new(databaseContext,
            Substitute.For<IBriefingAiClient>(),
            Substitute.For<IParseLogAiClient>(),
            Substitute.For<IPersonaAiClient>(),
            Substitute.For<IReadinessAiClient>());

    private static CompanyDbContext CreateAdminContext()
    {
        var systemTenantContext = new TenantContext();
        systemTenantContext.EnterSystemMode();

        var options = new DbContextOptionsBuilder<CompanyDbContext>()
            .UseNpgsql(LocalCompanyPostgresTestSettings.AdminConnectionString(LocalCompanyPostgresTestSettings.TestDatabaseName))
            .Options;

        return new CompanyDbContext(options, systemTenantContext);
    }

    private static CompanyDbContext CreateApplicationContext(Guid organizationId)
        => CreateApplicationContext(organizationId, out _);

    private static CompanyDbContext CreateApplicationContext(Guid organizationId, out TenantContext tenantContext)
    {
        tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId);

        var options = new DbContextOptionsBuilder<CompanyDbContext>()
            .UseNpgsql(LocalCompanyPostgresTestSettings.ApplicationRoleConnectionString())
            .AddInterceptors(
                new TenantSaveChangesInterceptor(tenantContext),
                new TenantConnectionInterceptor(tenantContext))
            .Options;

        return new CompanyDbContext(options, tenantContext);
    }

    private static async Task<IReadOnlyList<Guid>> ReadGuidsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;

        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private static async Task<TValue> ReadScalarAsAdminAsync<TValue>(string commandText, Guid id)
    {
        await using var connection = new NpgsqlConnection(
            LocalCompanyPostgresTestSettings.AdminConnectionString(LocalCompanyPostgresTestSettings.TestDatabaseName));
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("id", id);

        return (TValue)(await command.ExecuteScalarAsync())!;
    }

    private static async Task SetOrganizationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // Same statement TenantConnectionInterceptor issues on TransactionStarted. Its own builder
        // is internal to BuildingBlocks, so it is restated here; the GUC name is not, and comes from
        // the interceptor's public constant so a rename cannot silently desynchronise the two.
        command.CommandText =
            $"SET LOCAL {TenantConnectionInterceptor.OrganizationIdSettingName} = '{organizationId:D}'";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Seeded through the admin connection, which bypasses RLS — the same reason migrations keep
    /// working under the owning role in production. Writing the fixture through the application
    /// role would be circular: it could only ever create rows the policy already allows.
    ///
    /// <para>
    /// Three companies on purpose: two owned by different users inside organization A (the user
    /// half), one inside organization B (the organization half). Both A's first company and B's
    /// company have a follow-up due five minutes ago, so the reminder test has something to
    /// wrongly claim if the scoping regresses.
    /// </para>
    /// </summary>
    private static async Task SeedAsync(NpgsqlConnection connection)
    {
        await ExecuteAsync(connection, $"""
            INSERT INTO "Companies"
                ("Id", "OrganizationId", "UserId", "Name", "Description", "Status", "NextActionAt", "NextActionNote", "CreatedAt", "UpdatedAt") VALUES
                ('{FirstUserCompanyId}',          '{OrganizationAId}', '{FirstUserId}',              'My prospect',       '', 'Lead', now() - interval '5 minutes', 'Call about pricing', now(), now()),
                ('{ColleagueCompanyId}',          '{OrganizationAId}', '{ColleagueUserId}',          'Colleague prospect','', 'Lead', NULL,                          NULL,                 now(), now()),
                ('{OtherOrganizationCompanyId}',  '{OrganizationBId}', '{OtherOrganizationUserId}',  'Org B prospect',    '', 'Lead', now() - interval '5 minutes', 'Their note',         now(), now());

            INSERT INTO "CallLogEntries"
                ("Id", "OrganizationId", "CompanyId", "UserId", "ContactId", "ContactName", "Subject", "Outcome", "OccurredAt", "CreatedAt", "UpdatedAt") VALUES
                ('{FirstUserCallLogId}',         '{OrganizationAId}', '{FirstUserCompanyId}',         '{FirstUserId}',             NULL, 'Ivan',  'Intro', 'Callback', now(), now(), now()),
                ('{ColleagueCallLogId}',         '{OrganizationAId}', '{ColleagueCompanyId}',         '{ColleagueUserId}',         NULL, 'Petr',  'Intro', 'Callback', now(), now(), now()),
                ('{OtherOrganizationCallLogId}', '{OrganizationBId}', '{OtherOrganizationCompanyId}', '{OtherOrganizationUserId}', NULL, 'Sidor', 'Intro', 'Callback', now(), now(), now());

            INSERT INTO "CompanyContacts"
                ("Id", "OrganizationId", "CompanyId", "UserId", "Name", "Position", "Notes", "CreatedAt", "UpdatedAt") VALUES
                ('{FirstUserContactId}',         '{OrganizationAId}', '{FirstUserCompanyId}',         '{FirstUserId}',             'Ivan',  'CTO', '', now(), now()),
                ('{OtherOrganizationContactId}', '{OrganizationBId}', '{OtherOrganizationCompanyId}', '{OtherOrganizationUserId}', 'Sidor', 'CFO', '', now(), now());

            INSERT INTO "CompanyPersonas"
                ("Id", "OrganizationId", "CompanyId", "UserId", "Name", "Position", "Personality", "Difficulty", "CreatedAt") VALUES
                ('{FirstUserPersonaId}',         '{OrganizationAId}', '{FirstUserCompanyId}',         '{FirstUserId}',             'Ivan',  'CTO', 'Sceptical', 'Medium', now()),
                ('{OtherOrganizationPersonaId}', '{OrganizationBId}', '{OtherOrganizationCompanyId}', '{OtherOrganizationUserId}', 'Sidor', 'CFO', 'Sceptical', 'Medium', now());

            INSERT INTO "PracticeCalls"
                ("Id", "OrganizationId", "CompanyId", "UserId", "DialogSessionId", "Goal", "CreatedAt") VALUES
                ('{FirstUserPracticeCallId}',         '{OrganizationAId}', '{FirstUserCompanyId}',         '{FirstUserId}',             'session-a', 'Book a meeting', now()),
                ('{OtherOrganizationPracticeCallId}', '{OrganizationBId}', '{OtherOrganizationCompanyId}', '{OtherOrganizationUserId}', 'session-b', 'Book a meeting', now());
            """);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}
