using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.BuildingBlocks.Tests.Tenancy.Support;

namespace Sellevate.BuildingBlocks.Tests.Tenancy;

/// <summary>
/// Verifies the actual Postgres row-level-security boundary from docs/TENANCY/TENANCY.md §1.5 end
/// to end, against a real (local, throwaway) Postgres instance — the layer that survives raw SQL
/// and <c>ExecuteDelete</c>, which no in-memory-provider test can exercise.
///
/// <para>
/// Runs against the same local Docker Postgres <c>scripts/dev-infra.sh</c> already starts
/// (<c>localhost:5433</c> by default — see <see cref="LocalPostgresTestSettings"/>), inside its
/// own throwaway database (<see cref="LocalPostgresTestSettings.TestDatabaseName"/>) and a
/// test-only application role, both dropped and recreated on every run and dropped again at the
/// end. Nothing here ever touches the identity/learning/ai/company/gamification/social service
/// databases. Per CLAUDE.md, this must never hang or fail the build when no local Postgres is
/// reachable: <see cref="OneTimeSetUp"/> probes with a bounded timeout and every test skips
/// cleanly (<c>Assert.Ignore</c>) rather than failing when it is not. See
/// docs/TESTING/TENANCY.md for how to run this manually and what to look for.
/// </para>
/// </summary>
[TestFixture]
public class TenantRowLevelSecurityIntegrationTests
{
    private bool _isPostgresReachable;
    private Guid _organizationAId;
    private Guid _organizationBId;
    private Guid _organizationARowId;
    private Guid _organizationBRowId;

    [OneTimeSetUp]
    /// <summary>
    /// Builds the throwaway fixture: table, RLS policy, grants, seed rows.
    ///
    /// <para>
    /// The policy is applied by calling the shipped <c>EnableTenantRls</c> helper rather than a
    /// hand-copied re-statement of its SQL, so this fixture exercises exactly what a real service
    /// migration would run and cannot drift away from it.
    /// </para>
    ///
    /// <para>
    /// Seeding happens on the admin/superuser connection, which always bypasses RLS regardless of
    /// <c>FORCE</c> — the same reason migrations keep working under the owning role in production.
    /// </para>
    /// </summary>
    public async Task OneTimeSetUpAsync()
    {
        _isPostgresReachable = await LocalPostgresTestSettings.IsReachableAsync();
        if (!_isPostgresReachable)
        {
            return;
        }

        _organizationAId = Guid.NewGuid();
        _organizationBId = Guid.NewGuid();
        _organizationARowId = Guid.NewGuid();
        _organizationBRowId = Guid.NewGuid();

        await using (var maintenanceConnection = new NpgsqlConnection(LocalPostgresTestSettings.AdminConnectionString()))
        {
            await maintenanceConnection.OpenAsync();
            await ExecuteAsync(maintenanceConnection, $"""
                SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                WHERE datname = '{LocalPostgresTestSettings.TestDatabaseName}' AND pid <> pg_backend_pid();
                """);
            await ExecuteAsync(maintenanceConnection, $"DROP DATABASE IF EXISTS {LocalPostgresTestSettings.TestDatabaseName};");
            await ExecuteAsync(maintenanceConnection, $"DROP ROLE IF EXISTS {LocalPostgresTestSettings.ApplicationRoleName};");
            await ExecuteAsync(maintenanceConnection, $"CREATE DATABASE {LocalPostgresTestSettings.TestDatabaseName};");
            await ExecuteAsync(maintenanceConnection, $"""
                CREATE ROLE {LocalPostgresTestSettings.ApplicationRoleName}
                    WITH LOGIN PASSWORD '{LocalPostgresTestSettings.ApplicationRolePassword}'
                    NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
                """);
            await ExecuteAsync(maintenanceConnection, $"""
                GRANT CONNECT ON DATABASE {LocalPostgresTestSettings.TestDatabaseName} TO {LocalPostgresTestSettings.ApplicationRoleName};
                """);
        }

        await using var testDatabaseConnection = new NpgsqlConnection(LocalPostgresTestSettings.AdminConnectionString(LocalPostgresTestSettings.TestDatabaseName));
        await testDatabaseConnection.OpenAsync();

        await ExecuteAsync(testDatabaseConnection, $"""
            CREATE TABLE "{TenantRlsIntegrationDbContext.TableName}" (
                "Id" uuid PRIMARY KEY,
                "OrganizationId" uuid NOT NULL,
                "Name" text NOT NULL
            );
            """);

        await ExecuteAsync(testDatabaseConnection, BuildEnableTenantRlsSql());

        await ExecuteAsync(testDatabaseConnection, $"""
            GRANT USAGE ON SCHEMA public TO {LocalPostgresTestSettings.ApplicationRoleName};
            GRANT SELECT, INSERT, UPDATE, DELETE ON "{TenantRlsIntegrationDbContext.TableName}" TO {LocalPostgresTestSettings.ApplicationRoleName};
            """);

        await ExecuteAsync(testDatabaseConnection, $"""
            INSERT INTO "{TenantRlsIntegrationDbContext.TableName}" ("Id", "OrganizationId", "Name") VALUES
                ('{_organizationARowId}', '{_organizationAId}', 'BelongsToOrganizationA'),
                ('{_organizationBRowId}', '{_organizationBId}', 'BelongsToOrganizationB');
            """);
    }

    [OneTimeTearDown]
    /// <summary>
    /// Drops the throwaway database and role. Every step is best-effort and the <c>catch</c> is
    /// intentionally silent: teardown of a disposable fixture must never turn a passing run red.
    /// </summary>
    public async Task OneTimeTearDownAsync()
    {
        if (!_isPostgresReachable)
        {
            return;
        }

        try
        {
            await using var maintenanceConnection = new NpgsqlConnection(LocalPostgresTestSettings.AdminConnectionString());
            await maintenanceConnection.OpenAsync();
            await ExecuteAsync(maintenanceConnection, $"""
                SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                WHERE datname = '{LocalPostgresTestSettings.TestDatabaseName}' AND pid <> pg_backend_pid();
                """);
            await ExecuteAsync(maintenanceConnection, $"DROP DATABASE IF EXISTS {LocalPostgresTestSettings.TestDatabaseName};");
            await ExecuteAsync(maintenanceConnection, $"DROP ROLE IF EXISTS {LocalPostgresTestSettings.ApplicationRoleName};");
        }
        catch
        {
        }
    }

    [Test]
    public async Task Raw_sql_under_the_application_role_only_sees_the_current_organizations_rows()
    {
        SkipIfPostgresIsNotReachable();

        await using var connection = new NpgsqlConnection(LocalPostgresTestSettings.ApplicationRoleConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetOrganizationAsync(connection, transaction, _organizationAId);

        await using var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText = $"SELECT \"Id\" FROM \"{TenantRlsIntegrationDbContext.TableName}\" ORDER BY \"Id\";";
        var visibleRowIds = new List<Guid>();
        await using (var reader = await selectCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                visibleRowIds.Add(reader.GetGuid(0));
            }
        }

        visibleRowIds.Should().Equal(_organizationARowId);
        await transaction.CommitAsync();
    }

    [Test]
    public async Task Raw_sql_with_no_organization_setting_sees_zero_rows_fail_closed()
    {
        SkipIfPostgresIsNotReachable();

        await using var connection = new NpgsqlConnection(LocalPostgresTestSettings.ApplicationRoleConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText = $"SELECT count(*) FROM \"{TenantRlsIntegrationDbContext.TableName}\";";
        var visibleRowCount = (long)(await selectCommand.ExecuteScalarAsync())!;

        visibleRowCount.Should().Be(0);
        await transaction.CommitAsync();
    }

    [Test]
    /// <summary>
    /// Read paths — and <c>ExecuteDelete</c>, which bypasses the change tracker the same way a read
    /// does — have no implicit EF transaction, so per the <c>SET LOCAL</c> resolution documented in
    /// docs/DECISIONS.md the unit of work has to open one explicitly to give <c>SET LOCAL</c> a scope.
    /// That explicit <c>BeginTransactionAsync</c> is part of what is under test here, not boilerplate.
    /// </summary>
    public async Task ExecuteDelete_under_the_application_role_cannot_delete_another_organizations_row()
    {
        SkipIfPostgresIsNotReachable();

        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(_organizationAId);
        var interceptor = new TenantConnectionInterceptor(tenantContext);
        var optionsBuilder = new DbContextOptionsBuilder<TenantRlsIntegrationDbContext>()
            .UseNpgsql(LocalPostgresTestSettings.ApplicationRoleConnectionString())
            .AddInterceptors(interceptor);

        await using var context = new TenantRlsIntegrationDbContext(optionsBuilder.Options);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var deletedRowCount = await context.TenantRlsTestRows
            .Where(row => row.Id == _organizationBRowId)
            .ExecuteDeleteAsync();

        deletedRowCount.Should().Be(0);
        await transaction.CommitAsync();

        await AssertRowStillExistsAsync(_organizationBRowId);
    }

    [Test]
    public async Task Insert_under_the_application_role_cannot_write_a_foreign_organization_id()
    {
        SkipIfPostgresIsNotReachable();

        await using var connection = new NpgsqlConnection(LocalPostgresTestSettings.ApplicationRoleConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetOrganizationAsync(connection, transaction, _organizationAId);

        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = $"""
            INSERT INTO "{TenantRlsIntegrationDbContext.TableName}" ("Id", "OrganizationId", "Name")
            VALUES (@id, @organizationId, 'Smuggled');
            """;
        insertCommand.Parameters.AddWithValue("id", Guid.NewGuid());
        insertCommand.Parameters.AddWithValue("organizationId", _organizationBId);

        var act = async () => await insertCommand.ExecuteNonQueryAsync();

        var thrown = await act.Should().ThrowAsync<PostgresException>();
        thrown.Which.Message.Should().Contain("row-level security");
    }

    private void SkipIfPostgresIsNotReachable()
    {
        if (!_isPostgresReachable)
        {
            Assert.Ignore("Local Postgres is not reachable at localhost:5433 (or LOCAL_POSTGRES_PORT) — " +
                "start it with scripts/dev-infra.sh. Skipping the real-Postgres RLS integration test.");
        }
    }

    private async Task AssertRowStillExistsAsync(Guid rowId)
    {
        await using var connection = new NpgsqlConnection(LocalPostgresTestSettings.AdminConnectionString(LocalPostgresTestSettings.TestDatabaseName));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT count(*) FROM \"{TenantRlsIntegrationDbContext.TableName}\" WHERE \"Id\" = @id;";
        command.Parameters.AddWithValue("id", rowId);
        var rowCount = (long)(await command.ExecuteScalarAsync())!;
        rowCount.Should().Be(1);
    }

    private static async Task SetOrganizationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = TenantConnectionInterceptor.BuildSetLocalCommandText(organizationId);
        await command.ExecuteNonQueryAsync();
    }

    private static string BuildEnableTenantRlsSql()
    {
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql");
        migrationBuilder.EnableTenantRls(TenantRlsIntegrationDbContext.TableName);
        return ((SqlOperation)migrationBuilder.Operations.Single()).Sql;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}
