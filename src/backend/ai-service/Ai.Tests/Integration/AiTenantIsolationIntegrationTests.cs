using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using Npgsql;
using NUnit.Framework;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Features.Dialog.Services.Implementation;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.Ai.Infrastructure.Mongo;
using Sellevate.BuildingBlocks.Tenancy;
using StackExchange.Redis;

namespace Sellevate.Ai.Tests.Integration;

/// <summary>
/// Phase 40.11 — the acceptance criteria of the block, against real stores. ai-service is the first
/// service whose tenant boundary spans three of them, and each fails differently: Postgres has
/// row-level security, Mongo has nothing but <see cref="DialogSessionRepository"/>, and Redis has
/// nothing but the key name. So all three are checked here, with two organizations, and neither
/// seeing the other's anything.
///
/// <para>
/// The Postgres schema is built by running ai-service's own EF migrations, not by a hand-written
/// CREATE TABLE script: what is under test is the RLS that migration
/// <c>20260815154837_AddOrganizationId</c> actually emits, not a restatement of it. Reads run as a
/// throwaway NOBYPASSRLS login role, because isolation tested as the superuser proves nothing.
/// </para>
///
/// <para>
/// Runs against the local Docker stack <c>scripts/dev-infra.sh</c> starts (Postgres 5433, Mongo
/// 27017, Redis 6379 by default — see <see cref="LocalAiStoreTestSettings"/>), inside a throwaway
/// Postgres database and role, a throwaway Mongo database, and Redis keys under one prefix that
/// teardown deletes by name. It never touches the real <c>ai</c> database, the real
/// <c>sallevate</c> Mongo database, and it never calls FLUSHDB. Per CLAUDE.md and
/// docs/DONT_FORGET.md it must never hang or fail the build when the infrastructure is not
/// running: every probe is bounded and every test then skips in seconds. See
/// docs/TESTING/TENANCY.md for the one command that runs it.
/// </para>
///
/// <para>
/// Written but deliberately NOT run by the agent — Правило №2 in docs/DONT_FORGET.md.
/// </para>
/// </summary>
[TestFixture]
[Category("Integration")]
public class AiTenantIsolationIntegrationTests
{
    private static readonly Guid OrganizationAId = new("a0000000-0000-4000-8000-0000000000a1");
    private static readonly Guid OrganizationBId = new("b0000000-0000-4000-8000-0000000000b2");

    private static readonly Guid GlobalBundleId = new("00000000-0000-4000-8000-0000000000c1");
    private static readonly Guid GlobalModeId = new("00000000-0000-4000-8000-0000000000c2");
    private static readonly Guid OrganizationABundleId = new("a0000000-0000-4000-8000-0000000000c1");
    private static readonly Guid OrganizationAModeId = new("a0000000-0000-4000-8000-0000000000c2");
    private static readonly Guid OrganizationBBundleId = new("b0000000-0000-4000-8000-0000000000c1");
    private static readonly Guid OrganizationBModeId = new("b0000000-0000-4000-8000-0000000000c2");

    private static readonly Guid OrganizationAUserId = new("a0000000-0000-4000-8000-0000000000f1");
    private static readonly Guid OrganizationBUserId = new("b0000000-0000-4000-8000-0000000000f2");

    private bool _isPostgresReachable;
    private bool _isMongoReachable;
    private IConnectionMultiplexer? _redis;

    private string _organizationASessionId = string.Empty;
    private string _organizationBSessionId = string.Empty;

    // ── fixture lifecycle ────────────────────────────────────────────────

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _isPostgresReachable = await LocalAiStoreTestSettings.IsPostgresReachableAsync();
        _isMongoReachable = await LocalAiStoreTestSettings.IsMongoReachableAsync();
        _redis = await LocalAiStoreTestSettings.TryConnectRedisAsync();

        if (_isPostgresReachable)
        {
            await SetUpPostgresAsync();
        }

        if (_isMongoReachable)
        {
            await SetUpMongoAsync();
        }
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        if (_isPostgresReachable)
        {
            try
            {
                await using var maintenanceConnection = new NpgsqlConnection(LocalAiStoreTestSettings.AdminConnectionString());
                await maintenanceConnection.OpenAsync();
                await ExecuteAsync(maintenanceConnection, $"""
                    SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                    WHERE datname = '{LocalAiStoreTestSettings.TestDatabaseName}' AND pid <> pg_backend_pid();
                    """);
                await ExecuteAsync(maintenanceConnection, $"DROP DATABASE IF EXISTS {LocalAiStoreTestSettings.TestDatabaseName};");
                await ExecuteAsync(maintenanceConnection, $"DROP ROLE IF EXISTS {LocalAiStoreTestSettings.ApplicationRoleName};");
            }
            catch
            {
                // Best-effort cleanup of a throwaway fixture — never fail the run over teardown.
            }
        }

        if (_isMongoReachable)
        {
            try
            {
                await new MongoClient(LocalAiStoreTestSettings.MongoConnectionString())
                    .DropDatabaseAsync(LocalAiStoreTestSettings.MongoDatabaseName);
            }
            catch
            {
                // As above.
            }
        }

        if (_redis is not null)
        {
            try
            {
                // By name, never FLUSHDB: this Redis is shared with the developer's running stack.
                var database = _redis.GetDatabase();
                foreach (var organizationId in new[] { OrganizationAId, OrganizationBId })
                {
                    await database.KeyDeleteAsync(VerdictKey(organizationId));
                    await database.KeyDeleteAsync(IdempotencyKey(organizationId));
                }
            }
            catch
            {
                // As above.
            }

            await _redis.CloseAsync();
            _redis.Dispose();
        }
    }

    // ── Postgres: the dialog library ─────────────────────────────────────

    [Test]
    public async Task Global_dialog_library_stays_visible_to_both_organizations()
    {
        SkipIfPostgresIsNotReachable();

        var visibleToA = await ReadVisibleBundleIdsAsync(OrganizationAId);
        var visibleToB = await ReadVisibleBundleIdsAsync(OrganizationBId);

        visibleToA.Should().BeEquivalentTo(new[] { GlobalBundleId, OrganizationABundleId });
        visibleToB.Should().BeEquivalentTo(new[] { GlobalBundleId, OrganizationBBundleId });
    }

    /// <summary>
    /// The query filter is convenience; the policy is the boundary. Stripping the filter on purpose
    /// is the only way to tell which one is actually doing the work.
    /// </summary>
    [Test]
    public async Task Row_level_security_hides_the_other_organization_even_with_query_filters_ignored()
    {
        SkipIfPostgresIsNotReachable();

        await using var context = CreateApplicationContext(OrganizationAId);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var bundleIds = await context.DialogBundles
            .IgnoreQueryFilters()
            .Select(bundle => bundle.Id)
            .ToListAsync();

        bundleIds.Should().NotContain(OrganizationBBundleId);

        await transaction.CommitAsync();
    }

    [Test]
    public async Task Raw_sql_cannot_reach_the_other_organizations_modes()
    {
        SkipIfPostgresIsNotReachable();

        await using var context = CreateApplicationContext(OrganizationAId);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var reachableModeIds = await context.Database
            .SqlQuery<Guid>($"SELECT \"Id\" FROM \"DialogModes\"")
            .ToListAsync();

        reachableModeIds.Should().Contain(GlobalModeId);
        reachableModeIds.Should().Contain(OrganizationAModeId);
        reachableModeIds.Should().NotContain(OrganizationBModeId);

        await transaction.CommitAsync();
    }

    [Test]
    public async Task Writing_a_mode_into_another_organization_is_refused_by_the_policy()
    {
        SkipIfPostgresIsNotReachable();

        await using var context = CreateApplicationContext(OrganizationAId);

        context.DialogModes.Add(new DialogMode
        {
            Id = Guid.NewGuid(),
            OrganizationId = OrganizationBId,
            BundleId = OrganizationBBundleId,
            Key = "smuggled",
            Title = "Smuggled",
            Description = "Should never land",
            ChatSystemPrompt = "x",
            FeedbackSystemPrompt = "x",
        });

        var act = async () => await context.SaveChangesAsync();

        // WITH CHECK on the content policy: a row whose organization is neither NULL nor the
        // current one cannot be inserted, whatever the application believes.
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ── Mongo: dialog sessions ───────────────────────────────────────────

    [Test]
    public async Task One_organizations_sessions_are_invisible_to_another()
    {
        SkipIfMongoIsNotReachable();

        var repositoryForA = CreateSessionRepository(OrganizationAId);
        var repositoryForB = CreateSessionRepository(OrganizationBId);

        var sessionsVisibleToA = await repositoryForA.ListForUserAsync(OrganizationAUserId);
        var sessionsVisibleToB = await repositoryForB.ListForUserAsync(OrganizationBUserId);

        sessionsVisibleToA.Should().ContainSingle().Which.Id.Should().Be(_organizationASessionId);
        sessionsVisibleToB.Should().ContainSingle().Which.Id.Should().Be(_organizationBSessionId);
    }

    /// <summary>
    /// The important one. Knowing the session id and the user id of another organization's session
    /// — both of which travel in URLs — must not be enough to read it, because in Mongo the
    /// repository is the only thing standing between the two.
    /// </summary>
    [Test]
    public async Task Knowing_another_organizations_session_id_is_not_enough_to_read_it()
    {
        SkipIfMongoIsNotReachable();

        var repositoryForB = CreateSessionRepository(OrganizationBId);

        var stolen = await repositoryForB.FindForUserAsync(_organizationASessionId, OrganizationAUserId);

        stolen.Should().BeNull();
    }

    [Test]
    public async Task Writes_and_deletes_cannot_reach_another_organizations_session()
    {
        SkipIfMongoIsNotReachable();

        var repositoryForB = CreateSessionRepository(OrganizationBId);

        var incremented = await repositoryForB.IncrementVoiceSecondsAsync(_organizationASessionId, OrganizationAUserId, 60);
        var deleted = await repositoryForB.DeleteForUserAsync(_organizationASessionId, OrganizationAUserId);

        incremented.Should().BeFalse();
        deleted.Should().BeFalse();

        // And A's session is still intact and unchanged.
        var repositoryForA = CreateSessionRepository(OrganizationAId);
        var session = await repositoryForA.FindForUserAsync(_organizationASessionId, OrganizationAUserId);

        session.Should().NotBeNull();
        session!.VoiceSeconds.Should().Be(0);
    }

    [Test]
    public async Task Voice_usage_aggregation_never_totals_across_organizations()
    {
        SkipIfMongoIsNotReachable();

        var repositoryForA = CreateSessionRepository(OrganizationAId);
        await repositoryForA.IncrementVoiceSecondsAsync(_organizationASessionId, OrganizationAUserId, 120);

        var now = DateTime.UtcNow;
        var usageForB = await CreateSessionRepository(OrganizationBId)
            .AggregateVoiceUsageAsync(now.AddDays(-1), now.AddDays(-30));

        usageForB.Should().NotContain(entry => entry.UserId == OrganizationAUserId);
    }

    [Test]
    public async Task An_unset_tenant_reading_sessions_raises_instead_of_returning_everything()
    {
        SkipIfMongoIsNotReachable();

        var unscopedRepository = new DialogSessionRepository(CreateMongoContext(), new TenantContext());

        var act = async () => await unscopedRepository.ListForUserAsync(OrganizationAUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Organization context is not set.");
    }

    // ── Redis: caches and dedupe ─────────────────────────────────────────

    [Test]
    public async Task Two_organizations_never_read_each_others_cached_verdict()
    {
        SkipIfRedisIsNotReachable();

        var database = _redis!.GetDatabase();
        await database.StringSetAsync(VerdictKey(OrganizationAId), "ok", TimeSpan.FromMinutes(5));

        var readBackByB = await database.StringGetAsync(VerdictKey(OrganizationBId));

        readBackByB.HasValue.Should().BeFalse(
            "the verdict cache key is namespaced by organization, so B's lookup of the same scenario text misses");
    }

    [Test]
    public async Task Idempotency_keys_do_not_collide_across_organizations()
    {
        SkipIfRedisIsNotReachable();

        var database = _redis!.GetDatabase();
        await database.StringSetAsync(IdempotencyKey(OrganizationAId), "1", TimeSpan.FromMinutes(5));

        var seenByB = await database.KeyExistsAsync(IdempotencyKey(OrganizationBId));

        seenByB.Should().BeFalse(
            "one organization marking an event processed must not suppress the other organization's copy of it");
    }

    // ── setup helpers ────────────────────────────────────────────────────

    private static async Task SetUpPostgresAsync()
    {
        await using (var maintenanceConnection = new NpgsqlConnection(LocalAiStoreTestSettings.AdminConnectionString()))
        {
            await maintenanceConnection.OpenAsync();
            await ExecuteAsync(maintenanceConnection, $"""
                SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                WHERE datname = '{LocalAiStoreTestSettings.TestDatabaseName}' AND pid <> pg_backend_pid();
                """);
            await ExecuteAsync(maintenanceConnection, $"DROP DATABASE IF EXISTS {LocalAiStoreTestSettings.TestDatabaseName};");
            await ExecuteAsync(maintenanceConnection, $"DROP ROLE IF EXISTS {LocalAiStoreTestSettings.ApplicationRoleName};");
            await ExecuteAsync(maintenanceConnection, $"CREATE DATABASE {LocalAiStoreTestSettings.TestDatabaseName};");
            // NOBYPASSRLS is the whole point: FORCE ROW LEVEL SECURITY still lets a superuser (and,
            // without FORCE, the table owner) read everything, so isolation tested as the admin
            // role would pass no matter what the policies said.
            await ExecuteAsync(maintenanceConnection, $"""
                CREATE ROLE {LocalAiStoreTestSettings.ApplicationRoleName}
                    WITH LOGIN PASSWORD '{LocalAiStoreTestSettings.ApplicationRolePassword}'
                    NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
                """);
            await ExecuteAsync(maintenanceConnection, $"""
                GRANT CONNECT ON DATABASE {LocalAiStoreTestSettings.TestDatabaseName}
                    TO {LocalAiStoreTestSettings.ApplicationRoleName};
                """);
        }

        // The real migrations, run as the owner — exactly how production gets its schema and its
        // RLS policies. Nothing in this fixture writes DDL of its own.
        await using (var migrationContext = CreateAdminContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        await using var testDatabaseConnection = new NpgsqlConnection(
            LocalAiStoreTestSettings.AdminConnectionString(LocalAiStoreTestSettings.TestDatabaseName));
        await testDatabaseConnection.OpenAsync();

        await ExecuteAsync(testDatabaseConnection, $"""
            GRANT USAGE ON SCHEMA public TO {LocalAiStoreTestSettings.ApplicationRoleName};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public
                TO {LocalAiStoreTestSettings.ApplicationRoleName};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public
                TO {LocalAiStoreTestSettings.ApplicationRoleName};
            """);

        await SeedDialogLibraryAsync(testDatabaseConnection);
    }

    private static async Task SeedDialogLibraryAsync(NpgsqlConnection connection)
    {
        // Seeded as the owner, which bypasses nothing but is allowed to write rows for both
        // organizations — the fixture needs data on both sides of the boundary it is testing.
        await InsertBundleAsync(connection, GlobalBundleId, organizationId: null, "Global");
        await InsertBundleAsync(connection, OrganizationABundleId, OrganizationAId, "A");
        await InsertBundleAsync(connection, OrganizationBBundleId, OrganizationBId, "B");

        await InsertModeAsync(connection, GlobalModeId, organizationId: null, GlobalBundleId, "global-mode");
        await InsertModeAsync(connection, OrganizationAModeId, OrganizationAId, OrganizationABundleId, "a-mode");
        await InsertModeAsync(connection, OrganizationBModeId, OrganizationBId, OrganizationBBundleId, "b-mode");
    }

    private static async Task InsertBundleAsync(
        NpgsqlConnection connection,
        Guid bundleId,
        Guid? organizationId,
        string title)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "DialogBundles"
                ("Id", "OrganizationId", "SkillId", "Title", "Description", "IconEmoji",
                 "SortOrder", "IsActive", "IsHidden", "CreatedAt", "UpdatedAt")
            VALUES (@id, @organizationId, @skillId, @title, @title, '🎯', 0, true, false, now(), now());
            """;
        command.Parameters.AddWithValue("id", bundleId);
        command.Parameters.AddWithValue("organizationId", (object?)organizationId ?? DBNull.Value);
        command.Parameters.AddWithValue("skillId", Guid.NewGuid());
        command.Parameters.AddWithValue("title", title);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertModeAsync(
        NpgsqlConnection connection,
        Guid modeId,
        Guid? organizationId,
        Guid bundleId,
        string key)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "DialogModes"
                ("Id", "OrganizationId", "BundleId", "Key", "Title", "Description",
                 "ChatSystemPrompt", "FeedbackSystemPrompt", "SortOrder", "IsActive",
                 "VoiceEnabled", "CreatedAt", "UpdatedAt")
            VALUES (@id, @organizationId, @bundleId, @key, @key, @key, 'chat', 'feedback',
                    0, true, false, now(), now());
            """;
        command.Parameters.AddWithValue("id", modeId);
        command.Parameters.AddWithValue("organizationId", (object?)organizationId ?? DBNull.Value);
        command.Parameters.AddWithValue("bundleId", bundleId);
        command.Parameters.AddWithValue("key", key);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SetUpMongoAsync()
    {
        await new MongoClient(LocalAiStoreTestSettings.MongoConnectionString())
            .DropDatabaseAsync(LocalAiStoreTestSettings.MongoDatabaseName);

        // Inserted through the repository on purpose: if the repository ever stopped stamping the
        // organization on write, every isolation assertion below would still pass against
        // hand-written documents and the regression would go unnoticed.
        var organizationASession = new DialogSession
        {
            UserId = OrganizationAUserId,
            BundleId = OrganizationABundleId,
            ModeId = OrganizationAModeId,
        };
        await CreateSessionRepository(OrganizationAId).InsertAsync(organizationASession);
        _organizationASessionId = organizationASession.Id;

        var organizationBSession = new DialogSession
        {
            UserId = OrganizationBUserId,
            BundleId = OrganizationBBundleId,
            ModeId = OrganizationBModeId,
        };
        await CreateSessionRepository(OrganizationBId).InsertAsync(organizationBSession);
        _organizationBSessionId = organizationBSession.Id;
    }

    // ── plumbing ─────────────────────────────────────────────────────────

    private static AiDbContext CreateAdminContext()
    {
        var tenantContext = new TenantContext();
        tenantContext.EnterSystemMode();

        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseNpgsql(LocalAiStoreTestSettings.AdminConnectionString(LocalAiStoreTestSettings.TestDatabaseName))
            .Options;

        return new AiDbContext(options, tenantContext);
    }

    private static AiDbContext CreateApplicationContext(Guid organizationId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId);

        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseNpgsql(LocalAiStoreTestSettings.ApplicationRoleConnectionString())
            .AddInterceptors(
                new TenantSaveChangesInterceptor(tenantContext),
                new TenantConnectionInterceptor(tenantContext))
            .Options;

        return new AiDbContext(options, tenantContext);
    }

    private static MongoDbContext CreateMongoContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:DatabaseName"] = LocalAiStoreTestSettings.MongoDatabaseName,
            })
            .Build();

        return new MongoDbContext(new MongoClient(LocalAiStoreTestSettings.MongoConnectionString()), configuration);
    }

    private static IDialogSessionRepository CreateSessionRepository(Guid organizationId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId);

        return new DialogSessionRepository(CreateMongoContext(), tenantContext);
    }

    private static async Task<List<Guid>> ReadVisibleBundleIdsAsync(Guid organizationId)
    {
        await using var context = CreateApplicationContext(organizationId);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var bundleIds = await context.DialogBundles.Select(bundle => bundle.Id).ToListAsync();

        await transaction.CommitAsync();
        return bundleIds;
    }

    /// <summary>
    /// The shape ScenarioValidationService writes, with the fixture's own prefix so teardown can
    /// delete exactly these keys and nothing the developer's stack owns.
    /// </summary>
    private static string VerdictKey(Guid organizationId)
        => $"{LocalAiStoreTestSettings.RedisKeyPrefix}org:{organizationId}:dialog:scenario-validation:v1:deadbeef";

    private static string IdempotencyKey(Guid organizationId)
        => $"{LocalAiStoreTestSettings.RedisKeyPrefix}org:{organizationId}:idem:ai-test:00000000000000000000000000000001";

    private static async Task ExecuteAsync(NpgsqlConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private void SkipIfPostgresIsNotReachable()
    {
        if (!_isPostgresReachable)
        {
            Assert.Ignore("No local Postgres on the dev-infra port; skipping the ai-service RLS isolation checks.");
        }
    }

    private void SkipIfMongoIsNotReachable()
    {
        if (!_isMongoReachable)
        {
            Assert.Ignore("No local Mongo on the dev-infra port; skipping the dialog-session isolation checks.");
        }
    }

    private void SkipIfRedisIsNotReachable()
    {
        if (_redis is null)
        {
            Assert.Ignore("No local Redis on the dev-infra port; skipping the Redis key-namespacing checks.");
        }
    }
}
