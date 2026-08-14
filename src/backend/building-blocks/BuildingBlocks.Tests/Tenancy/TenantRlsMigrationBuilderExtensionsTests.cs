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

    private static string SingleSqlOperationText(MigrationBuilder migrationBuilder)
    {
        var operation = migrationBuilder.Operations.Should().ContainSingle().Which;
        return operation.Should().BeOfType<SqlOperation>().Which.Sql;
    }
}
