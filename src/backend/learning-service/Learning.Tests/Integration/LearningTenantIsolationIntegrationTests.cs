using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Tests.Integration;

/// <summary>
/// Phase 40.10 — the point of the whole block: two organizations, and neither can reach the other's
/// progress through any door. Verified against a real Postgres, because the doors that matter are
/// the ones the EF query filter does not guard — a navigation property, raw SQL, and
/// <c>ExecuteUpdate</c> / <c>ExecuteDelete</c>, none of which the in-memory provider can model.
///
/// <para>
/// The schema is built by running learning-service's own EF migrations, not by a hand-written
/// CREATE TABLE script: what is under test is the row-level security that migration
/// <c>20260815152225_AddOrganizationId</c> actually emits, not a re-statement of it.
/// </para>
///
/// <para>
/// Runs against the same local Docker Postgres <c>scripts/dev-infra.sh</c> starts
/// (<c>localhost:5433</c> by default — see <see cref="LocalLearningPostgresTestSettings"/>), inside
/// its own throwaway database and a throwaway NOBYPASSRLS login role, both dropped and recreated at
/// the start of the run and dropped again at the end. It never touches the real <c>learning</c>
/// database. Per CLAUDE.md and docs/DONT_FORGET.md it must never hang or fail the build when no
/// local Postgres is reachable: <see cref="OneTimeSetUpAsync"/> probes with a bounded timeout and
/// every test then skips in seconds via <c>Assert.Ignore</c>. See docs/TESTING/TENANCY.md for the
/// one command that runs it and what to look for.
/// </para>
/// </summary>
[TestFixture]
[Category("Integration")]
public class LearningTenantIsolationIntegrationTests
{
    private const string EmptyJsonObject = "{}";

    private bool _isPostgresReachable;

    private static readonly Guid OrganizationAId = new("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid OrganizationBId = new("b0000000-0000-4000-8000-000000000002");

    private static readonly Guid GlobalSkillId = new("00000000-0000-4000-8000-00000000000a");
    private static readonly Guid GlobalTopicId = new("00000000-0000-4000-8000-00000000000b");
    private static readonly Guid GlobalLessonId = new("00000000-0000-4000-8000-00000000000c");
    private static readonly Guid GlobalExerciseId = new("00000000-0000-4000-8000-00000000000d");

    private static readonly Guid OrganizationASkillId = new("a0000000-0000-4000-8000-00000000000a");
    private static readonly Guid OrganizationATopicId = new("a0000000-0000-4000-8000-00000000000b");
    private static readonly Guid OrganizationALessonId = new("a0000000-0000-4000-8000-00000000000c");
    private static readonly Guid OrganizationAExerciseId = new("a0000000-0000-4000-8000-00000000000d");

    private static readonly Guid OrganizationBSkillId = new("b0000000-0000-4000-8000-00000000000a");
    private static readonly Guid OrganizationBTopicId = new("b0000000-0000-4000-8000-00000000000b");
    private static readonly Guid OrganizationBLessonId = new("b0000000-0000-4000-8000-00000000000c");
    private static readonly Guid OrganizationBExerciseId = new("b0000000-0000-4000-8000-00000000000d");

    private static readonly Guid OrganizationAUserId = new("a0000000-0000-4000-8000-0000000000f1");
    private static readonly Guid OrganizationBUserId = new("b0000000-0000-4000-8000-0000000000f2");

    private static readonly Guid OrganizationALessonProgressId = new("a0000000-0000-4000-8000-0000000000e1");
    private static readonly Guid OrganizationBLessonProgressId = new("b0000000-0000-4000-8000-0000000000e2");
    private static readonly Guid OrganizationAAttemptId = new("a0000000-0000-4000-8000-0000000000e3");
    private static readonly Guid OrganizationBAttemptId = new("b0000000-0000-4000-8000-0000000000e4");

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _isPostgresReachable = await LocalLearningPostgresTestSettings.IsReachableAsync();
        if (!_isPostgresReachable)
        {
            return;
        }

        await using (var maintenanceConnection = new NpgsqlConnection(LocalLearningPostgresTestSettings.AdminConnectionString()))
        {
            await maintenanceConnection.OpenAsync();
            await ExecuteAsync(maintenanceConnection, $"""
                SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                WHERE datname = '{LocalLearningPostgresTestSettings.TestDatabaseName}' AND pid <> pg_backend_pid();
                """);
            await ExecuteAsync(maintenanceConnection, $"DROP DATABASE IF EXISTS {LocalLearningPostgresTestSettings.TestDatabaseName};");
            await ExecuteAsync(maintenanceConnection, $"DROP ROLE IF EXISTS {LocalLearningPostgresTestSettings.ApplicationRoleName};");
            await ExecuteAsync(maintenanceConnection, $"CREATE DATABASE {LocalLearningPostgresTestSettings.TestDatabaseName};");
            // NOBYPASSRLS is the whole point: FORCE ROW LEVEL SECURITY still lets a superuser (and,
            // without FORCE, the table owner) read everything, so isolation tested as the admin
            // role would pass no matter what the policies said.
            await ExecuteAsync(maintenanceConnection, $"""
                CREATE ROLE {LocalLearningPostgresTestSettings.ApplicationRoleName}
                    WITH LOGIN PASSWORD '{LocalLearningPostgresTestSettings.ApplicationRolePassword}'
                    NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
                """);
            await ExecuteAsync(maintenanceConnection, $"""
                GRANT CONNECT ON DATABASE {LocalLearningPostgresTestSettings.TestDatabaseName}
                    TO {LocalLearningPostgresTestSettings.ApplicationRoleName};
                """);
        }

        // The real migrations, run as the owner — exactly how production gets its schema and its
        // RLS policies. Nothing in this fixture writes DDL of its own.
        await using (var migrationContext = CreateAdminContext())
        {
            await migrationContext.Database.MigrateAsync();
        }

        await using var testDatabaseConnection = new NpgsqlConnection(
            LocalLearningPostgresTestSettings.AdminConnectionString(LocalLearningPostgresTestSettings.TestDatabaseName));
        await testDatabaseConnection.OpenAsync();

        await ExecuteAsync(testDatabaseConnection, $"""
            GRANT USAGE ON SCHEMA public TO {LocalLearningPostgresTestSettings.ApplicationRoleName};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public
                TO {LocalLearningPostgresTestSettings.ApplicationRoleName};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public
                TO {LocalLearningPostgresTestSettings.ApplicationRoleName};
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
            await using var maintenanceConnection = new NpgsqlConnection(LocalLearningPostgresTestSettings.AdminConnectionString());
            await maintenanceConnection.OpenAsync();
            await ExecuteAsync(maintenanceConnection, $"""
                SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                WHERE datname = '{LocalLearningPostgresTestSettings.TestDatabaseName}' AND pid <> pg_backend_pid();
                """);
            await ExecuteAsync(maintenanceConnection, $"DROP DATABASE IF EXISTS {LocalLearningPostgresTestSettings.TestDatabaseName};");
            await ExecuteAsync(maintenanceConnection, $"DROP ROLE IF EXISTS {LocalLearningPostgresTestSettings.ApplicationRoleName};");
        }
        catch
        {
            // Best-effort cleanup of a throwaway fixture — never fail the run over teardown.
        }
    }

    /// <summary>
    /// The trap docs/TENANCY/TENANCY.md §1.4 calls out by name: query filters are not inherited
    /// through navigations. Reading exercises with the whole Skill -> Topic -> Lesson -> Exercise
    /// chain eagerly loaded must still exclude the other organization at every level, not just at
    /// the root the query started from.
    /// </summary>
    [Test]
    public async Task Content_read_through_the_navigation_chain_never_crosses_the_organization_boundary()
    {
        SkipIfPostgresIsNotReachable();

        await using var context = CreateApplicationContext(OrganizationAId);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var exercises = await context.Exercises
            .Include(exercise => exercise.Lesson)
                .ThenInclude(lesson => lesson!.Topic)
                    .ThenInclude(topic => topic!.Skill)
            .ToListAsync();

        exercises.Select(exercise => exercise.Id)
            .Should().BeEquivalentTo(new[] { GlobalExerciseId, OrganizationAExerciseId });

        // Every hop of the chain, not only the entity the query started from.
        exercises.Select(exercise => exercise.Lesson!.Id)
            .Should().NotContain(OrganizationBLessonId);
        exercises.Select(exercise => exercise.Lesson!.Topic!.Id)
            .Should().NotContain(OrganizationBTopicId);
        exercises.Select(exercise => exercise.Lesson!.Topic!.Skill!.Id)
            .Should().NotContain(OrganizationBSkillId);

        await transaction.CommitAsync();
    }

    [Test]
    public async Task Global_content_stays_visible_to_both_organizations()
    {
        SkipIfPostgresIsNotReachable();

        var visibleToA = await ReadVisibleSkillIdsAsync(OrganizationAId);
        var visibleToB = await ReadVisibleSkillIdsAsync(OrganizationBId);

        visibleToA.Should().BeEquivalentTo(new[] { GlobalSkillId, OrganizationASkillId });
        visibleToB.Should().BeEquivalentTo(new[] { GlobalSkillId, OrganizationBSkillId });
    }

    /// <summary>
    /// Raw SQL is the door the EF query filter does not guard at all. This is the layer
    /// docs/TENANCY/TENANCY.md §1.5 exists for.
    /// </summary>
    [Test]
    public async Task Raw_sql_only_returns_the_current_organizations_progress()
    {
        SkipIfPostgresIsNotReachable();

        await using var connection = new NpgsqlConnection(LocalLearningPostgresTestSettings.ApplicationRoleConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetOrganizationAsync(connection, transaction, OrganizationAId);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """SELECT "Id" FROM "UserExerciseAttempts" ORDER BY "Id";""";

        var visibleAttemptIds = new List<Guid>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                visibleAttemptIds.Add(reader.GetGuid(0));
            }
        }

        visibleAttemptIds.Should().Equal(OrganizationAAttemptId);
        await transaction.CommitAsync();
    }

    [Test]
    public async Task Raw_sql_with_no_organization_setting_sees_no_progress_at_all()
    {
        SkipIfPostgresIsNotReachable();

        await using var connection = new NpgsqlConnection(LocalLearningPostgresTestSettings.ApplicationRoleConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT (SELECT count(*) FROM "UserExerciseAttempts")
                 + (SELECT count(*) FROM "UserLessonProgressRecords")
                 + (SELECT count(*) FROM "UserSkillProgressRecords")
                 + (SELECT count(*) FROM "UserTechniqueProgress");
            """;

        var visibleRowCount = (long)(await command.ExecuteScalarAsync())!;

        visibleRowCount.Should().Be(0, "an unset tenant must mean no data, never all data");
        await transaction.CommitAsync();
    }

    /// <summary>
    /// Global content, by contrast, is readable with no organization set — that is the difference
    /// between <c>EnableTenantRls</c> and <c>EnableTenantRlsForContent</c>, and it is why the
    /// superadmin-only admin controllers keep working without a tenant scope in 40.10.
    /// </summary>
    [Test]
    public async Task Raw_sql_with_no_organization_setting_still_sees_the_global_library()
    {
        SkipIfPostgresIsNotReachable();

        await using var connection = new NpgsqlConnection(LocalLearningPostgresTestSettings.ApplicationRoleConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """SELECT "Id" FROM "Skills" ORDER BY "Id";""";

        var visibleSkillIds = new List<Guid>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                visibleSkillIds.Add(reader.GetGuid(0));
            }
        }

        visibleSkillIds.Should().Equal(GlobalSkillId);
        await transaction.CommitAsync();
    }

    [Test]
    public async Task ExecuteUpdate_cannot_touch_another_organizations_progress()
    {
        SkipIfPostgresIsNotReachable();

        await using (var context = CreateApplicationContext(OrganizationAId))
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            // IgnoreQueryFilters strips the EF-level guard on purpose: what is under test is what
            // survives when the convenience layer is gone.
            var updatedRowCount = await context.UserLessonProgressRecords
                .IgnoreQueryFilters()
                .Where(progress => progress.Id == OrganizationBLessonProgressId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(progress => progress.BestScore, 1));

            updatedRowCount.Should().Be(0);
            await transaction.CommitAsync();
        }

        var survivingScore = await ReadScalarAsAdminAsync<int>(
            """SELECT "BestScore" FROM "UserLessonProgressRecords" WHERE "Id" = @id;""",
            OrganizationBLessonProgressId);

        survivingScore.Should().Be(77, "organization B's score must be exactly as it was seeded");
    }

    [Test]
    public async Task ExecuteDelete_cannot_remove_another_organizations_progress()
    {
        SkipIfPostgresIsNotReachable();

        await using (var context = CreateApplicationContext(OrganizationAId))
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            var deletedRowCount = await context.UserExerciseAttempts
                .IgnoreQueryFilters()
                .Where(attempt => attempt.Id == OrganizationBAttemptId)
                .ExecuteDeleteAsync();

            deletedRowCount.Should().Be(0);
            await transaction.CommitAsync();
        }

        var survivingRowCount = await ReadScalarAsAdminAsync<long>(
            """SELECT count(*) FROM "UserExerciseAttempts" WHERE "Id" = @id;""",
            OrganizationBAttemptId);

        survivingRowCount.Should().Be(1);
    }

    [Test]
    public async Task Inserting_progress_for_another_organization_is_refused_by_postgres()
    {
        SkipIfPostgresIsNotReachable();

        await using var connection = new NpgsqlConnection(LocalLearningPostgresTestSettings.ApplicationRoleConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetOrganizationAsync(connection, transaction, OrganizationAId);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO "UserExerciseAttempts"
                ("Id", "OrganizationId", "UserId", "ExerciseId", "SerializedAnswer", "IsCorrect", "Score", "AttemptedAt")
            VALUES (@id, @organizationId, @userId, @exerciseId, '{}', true, 100, now());
            """;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("organizationId", OrganizationBId);
        command.Parameters.AddWithValue("userId", OrganizationBUserId);
        command.Parameters.AddWithValue("exerciseId", GlobalExerciseId);

        var act = async () => await command.ExecuteNonQueryAsync();

        var thrown = await act.Should().ThrowAsync<PostgresException>();
        thrown.Which.Message.Should().Contain("row-level security");
    }

    /// <summary>
    /// The failure mode the service's own read path would hit if a method forgot its
    /// <c>TenantTransactionScope</c>: <c>SET LOCAL</c> needs a transaction, so a bare SELECT sees
    /// nothing. Asserting it here keeps the reason for that scope rule from being forgotten.
    /// </summary>
    [Test]
    public async Task A_read_without_a_transaction_sees_no_progress_even_with_a_tenant_context()
    {
        SkipIfPostgresIsNotReachable();

        await using var context = CreateApplicationContext(OrganizationAId);

        var visibleAttempts = await context.UserExerciseAttempts.ToListAsync();

        visibleAttempts.Should().BeEmpty(
            "SET LOCAL has no effect outside a transaction, so the RLS policy filters everything — "
            + "this is why every learning-service read path opens a TenantTransactionScope");
    }

    private void SkipIfPostgresIsNotReachable()
    {
        if (!_isPostgresReachable)
        {
            Assert.Ignore(
                "Local Postgres is not reachable at localhost:5433 (or LOCAL_POSTGRES_PORT) — "
                + "start it with scripts/dev-infra.sh. Skipping the learning-service tenant-isolation test.");
        }
    }

    private static LearningDbContext CreateAdminContext()
    {
        var systemTenantContext = new TenantContext();
        systemTenantContext.EnterSystemMode();

        var options = new DbContextOptionsBuilder<LearningDbContext>()
            .UseNpgsql(LocalLearningPostgresTestSettings.AdminConnectionString(LocalLearningPostgresTestSettings.TestDatabaseName))
            .Options;

        return new LearningDbContext(options, systemTenantContext);
    }

    private static LearningDbContext CreateApplicationContext(Guid organizationId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId);

        var options = new DbContextOptionsBuilder<LearningDbContext>()
            .UseNpgsql(LocalLearningPostgresTestSettings.ApplicationRoleConnectionString())
            .AddInterceptors(
                new TenantSaveChangesInterceptor(tenantContext),
                new TenantConnectionInterceptor(tenantContext))
            .Options;

        return new LearningDbContext(options, tenantContext);
    }

    private static async Task<IReadOnlyList<Guid>> ReadVisibleSkillIdsAsync(Guid organizationId)
    {
        await using var context = CreateApplicationContext(organizationId);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var skillIds = await context.Skills.Select(skill => skill.Id).ToListAsync();

        await transaction.CommitAsync();
        return skillIds;
    }

    private static async Task<TValue> ReadScalarAsAdminAsync<TValue>(string commandText, Guid id)
    {
        await using var connection = new NpgsqlConnection(
            LocalLearningPostgresTestSettings.AdminConnectionString(LocalLearningPostgresTestSettings.TestDatabaseName));
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
    /// </summary>
    private static async Task SeedAsync(NpgsqlConnection connection)
    {
        await ExecuteAsync(connection, $"""
            INSERT INTO "Skills" ("Id", "OrganizationId", "IconicName", "OrderInTree", "Title", "Stage") VALUES
                ('{GlobalSkillId}',       NULL,               'objections', 1, 'Objections (global)', 'general'),
                ('{OrganizationASkillId}','{OrganizationAId}','objections', 1, 'Objections (A)',      'general'),
                ('{OrganizationBSkillId}','{OrganizationBId}','objections', 1, 'Objections (B)',      'general');

            INSERT INTO "Topics" ("Id", "OrganizationId", "SkillId", "IconicName", "OrderInSkill", "Title") VALUES
                ('{GlobalTopicId}',       NULL,               '{GlobalSkillId}',        'price', 1, 'Price (global)'),
                ('{OrganizationATopicId}','{OrganizationAId}','{OrganizationASkillId}', 'price', 1, 'Price (A)'),
                ('{OrganizationBTopicId}','{OrganizationBId}','{OrganizationBSkillId}', 'price', 1, 'Price (B)');

            INSERT INTO "Lessons" ("Id", "OrganizationId", "TopicId", "OrderInTopic", "Title") VALUES
                ('{GlobalLessonId}',       NULL,               '{GlobalTopicId}',        1, 'Lesson (global)'),
                ('{OrganizationALessonId}','{OrganizationAId}','{OrganizationATopicId}', 1, 'Lesson (A)'),
                ('{OrganizationBLessonId}','{OrganizationBId}','{OrganizationBTopicId}', 1, 'Lesson (B)');

            INSERT INTO "Exercises" ("Id", "OrganizationId", "LessonId", "Type", "OrderInLesson", "SerializedContent", "CreatedAt", "UpdatedAt") VALUES
                ('{GlobalExerciseId}',       NULL,               '{GlobalLessonId}',        'choose_option', 1, '{EmptyJsonObject}', now(), now()),
                ('{OrganizationAExerciseId}','{OrganizationAId}','{OrganizationALessonId}', 'choose_option', 1, '{EmptyJsonObject}', now(), now()),
                ('{OrganizationBExerciseId}','{OrganizationBId}','{OrganizationBLessonId}', 'choose_option', 1, '{EmptyJsonObject}', now(), now());

            INSERT INTO "UserLessonProgressRecords" ("Id", "OrganizationId", "UserId", "LessonId", "Status", "BestScore") VALUES
                ('{OrganizationALessonProgressId}', '{OrganizationAId}', '{OrganizationAUserId}', '{GlobalLessonId}', 'completed', 55),
                ('{OrganizationBLessonProgressId}', '{OrganizationBId}', '{OrganizationBUserId}', '{GlobalLessonId}', 'completed', 77);

            INSERT INTO "UserExerciseAttempts" ("Id", "OrganizationId", "UserId", "ExerciseId", "SerializedAnswer", "IsCorrect", "Score", "AttemptedAt") VALUES
                ('{OrganizationAAttemptId}', '{OrganizationAId}', '{OrganizationAUserId}', '{GlobalExerciseId}', '{EmptyJsonObject}', true,  100, now()),
                ('{OrganizationBAttemptId}', '{OrganizationBId}', '{OrganizationBUserId}', '{GlobalExerciseId}', '{EmptyJsonObject}', false, 20,  now());
            """);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}
