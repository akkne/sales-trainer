using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.BuildingBlocks.Tests.Tenancy;

[TestFixture]
public class TenantRlsMigrationBuilderExtensionsTests
{
    [Test]
    public void EnableTenantRls_enables_and_forces_row_level_security()
    {
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql");

        migrationBuilder.EnableTenantRls("UserExerciseAttempts");

        var sql = SingleSqlOperationText(migrationBuilder);
        sql.Should().Contain("ALTER TABLE \"UserExerciseAttempts\" ENABLE ROW LEVEL SECURITY;");
        sql.Should().Contain("ALTER TABLE \"UserExerciseAttempts\" FORCE ROW LEVEL SECURITY;");
    }

    [Test]
    public void EnableTenantRls_policy_has_both_using_and_with_check()
    {
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql");

        migrationBuilder.EnableTenantRls("UserExerciseAttempts");

        var sql = SingleSqlOperationText(migrationBuilder);
        sql.Should().Contain("USING (");
        sql.Should().Contain("WITH CHECK (");
    }

    [Test]
    public void EnableTenantRls_reads_the_organization_setting_with_missing_ok_so_an_unset_value_fails_closed()
    {
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql");

        migrationBuilder.EnableTenantRls("UserExerciseAttempts");

        var sql = SingleSqlOperationText(migrationBuilder);
        sql.Should().Contain("current_setting('app.organization_id', true)");
    }

    [Test]
    public void EnableTenantRls_treats_an_empty_string_setting_as_unset_not_as_an_invalid_uuid()
    {
        // A pooled physical connection that has ever run SET LOCAL reverts the GUC to '' (not
        // NULL) once that transaction ends — confirmed against a real Postgres instance. Without
        // NULLIF, ''::uuid raises an error instead of filtering to zero rows (not fail closed).
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql");

        migrationBuilder.EnableTenantRls("UserExerciseAttempts");

        var sql = SingleSqlOperationText(migrationBuilder);
        sql.Should().Contain("NULLIF(current_setting('app.organization_id', true), '')::uuid");
    }

    [Test]
    public void EnableTenantRls_compares_the_organization_id_column_by_default()
    {
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql");

        migrationBuilder.EnableTenantRls("UserExerciseAttempts");

        var sql = SingleSqlOperationText(migrationBuilder);
        sql.Should().Contain("\"OrganizationId\" = NULLIF(current_setting('app.organization_id', true), '')::uuid");
    }

    [Test]
    public void EnableTenantRlsForContent_treats_a_null_organization_id_as_global()
    {
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql");

        migrationBuilder.EnableTenantRlsForContent("Skills");

        var sql = SingleSqlOperationText(migrationBuilder);
        sql.Should().Contain("\"OrganizationId\" IS NULL OR \"OrganizationId\" = NULLIF(current_setting('app.organization_id', true), '')::uuid");
    }

    [Test]
    public void EnableTenantRls_quotes_the_generated_policy_name()
    {
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql");

        migrationBuilder.EnableTenantRls("UserExerciseAttempts");

        var sql = SingleSqlOperationText(migrationBuilder);
        sql.Should().Contain("CREATE POLICY \"UserExerciseAttempts_tenant_isolation\" ON \"UserExerciseAttempts\"");
    }

    /// <summary>
    /// The asymmetry is the whole design of the 2026-08-16 role split: platform staff read every
    /// organization's rows and still write into none of them implicitly. A test that only checked
    /// "the platform branch is present somewhere" would pass on a policy that had leaked it into
    /// <c>WITH CHECK</c> too, so this one splits the statement and asserts on each half.
    /// </summary>
    [Test]
    public void EnableTenantRls_admits_platform_staff_to_reads_but_not_to_writes()
    {
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql");

        migrationBuilder.EnableTenantRls("UserExerciseAttempts");

        var sql = SingleSqlOperationText(migrationBuilder);
        UsingClauseOf(sql).Should().Contain("current_setting('app.platform_mode', true)");
        WithCheckClauseOf(sql).Should().NotContain("platform_mode");
    }

    [Test]
    public void EnableTenantRlsForContent_admits_platform_staff_to_reads_but_not_to_writes()
    {
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql");

        migrationBuilder.EnableTenantRlsForContent("Skills");

        var sql = SingleSqlOperationText(migrationBuilder);
        UsingClauseOf(sql).Should().Contain("current_setting('app.platform_mode', true)");
        WithCheckClauseOf(sql).Should().NotContain("platform_mode");
    }

    [Test]
    public void EnableTenantRls_treats_an_empty_platform_setting_as_off()
    {
        // Same pooled-connection trap as the organization GUC: once SET LOCAL has run on a
        // connection, the setting comes back as '' rather than NULL for the next transaction.
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql");

        migrationBuilder.EnableTenantRls("UserExerciseAttempts");

        var sql = SingleSqlOperationText(migrationBuilder);
        sql.Should().Contain("COALESCE(NULLIF(current_setting('app.platform_mode', true), ''), 'off') = 'on'");
    }

    /// <summary>
    /// What a rollback migration calls. It must regenerate the pre-2026-08-16 policy exactly, with
    /// no trace of the platform branch in either clause.
    /// </summary>
    [Test]
    public void EnableTenantRls_without_platform_staff_emits_the_pre_role_split_policy()
    {
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql");

        migrationBuilder.EnableTenantRls("UserExerciseAttempts", admitPlatformStaff: false);

        var sql = SingleSqlOperationText(migrationBuilder);
        sql.Should().NotContain("platform_mode");
        sql.Should().Contain("\"OrganizationId\" = NULLIF(current_setting('app.organization_id', true), '')::uuid");
    }

    /// <summary>
    /// Re-applying the helper to a table that already carries the policy has to replace it, not
    /// fail — that is what lets a later migration re-issue the current definition instead of
    /// hand-writing a second copy of the policy SQL that would drift from the helper.
    /// </summary>
    [Test]
    public void EnableTenantRls_replaces_an_existing_policy_instead_of_failing_on_it()
    {
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql");

        migrationBuilder.EnableTenantRls("UserExerciseAttempts");

        var sql = SingleSqlOperationText(migrationBuilder);
        sql.Should().Contain("DROP POLICY IF EXISTS \"UserExerciseAttempts_tenant_isolation\" ON \"UserExerciseAttempts\";");
        sql.IndexOf("DROP POLICY", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("CREATE POLICY", StringComparison.Ordinal));
    }

    private static string UsingClauseOf(string sql)
    {
        var start = sql.IndexOf("USING (", StringComparison.Ordinal);
        var end = sql.IndexOf("WITH CHECK (", StringComparison.Ordinal);
        return sql[start..end];
    }

    private static string WithCheckClauseOf(string sql)
        => sql[sql.IndexOf("WITH CHECK (", StringComparison.Ordinal)..];

    private static string SingleSqlOperationText(MigrationBuilder migrationBuilder)
    {
        var operation = migrationBuilder.Operations.Should().ContainSingle().Which;
        return operation.Should().BeOfType<SqlOperation>().Which.Sql;
    }
}
