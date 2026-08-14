using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace Sellevate.BuildingBlocks.Tenancy;

/// <summary>
/// Migration-time helper that turns on Postgres row-level security for a tenant-scoped table, per
/// docs/TENANCY/TENANCY.md §1.5. Called from a service's own EF migration once that service starts
/// adding <c>OrganizationId</c> to a table (Stage C, 40.10+) — this building block only ships the
/// mechanism, it enables RLS on nothing by itself.
/// </summary>
public static class TenantRlsMigrationBuilderExtensions
{
    public const string DefaultOrganizationIdColumnName = "OrganizationId";

    /// <summary>
    /// Enables RLS for a table where every row belongs to exactly one organization
    /// (<c>OrganizationId NOT NULL</c>) — user progress, attempts, dialog sessions, assignments,
    /// notifications, etc. (TENANCY.md §1.2, "Tenant data").
    ///
    /// Emits <c>ENABLE</c> + <c>FORCE</c> ROW LEVEL SECURITY and a policy with both
    /// <c>USING</c> and <c>WITH CHECK</c>, reading
    /// <c>NULLIF(current_setting('app.organization_id', true), '')</c> so an unset session
    /// variable yields zero rows rather than an error — fail closed. The <c>NULLIF</c> is not
    /// redundant with <c>missing_ok</c>: once a pooled physical connection has run
    /// <c>SET LOCAL app.organization_id = ...</c> even once, Postgres reverts the GUC to <c>''</c>
    /// (not NULL) when that transaction ends, and <c>''::uuid</c> raises an error rather than
    /// filtering to zero rows — confirmed against a real Postgres instance, see
    /// docs/DECISIONS.md ("SET LOCAL vs SET", 2026-08-15).
    /// </summary>
    public static OperationBuilder<SqlOperation> EnableTenantRls(
        this MigrationBuilder migrationBuilder,
        string tableName,
        string organizationIdColumnName = DefaultOrganizationIdColumnName)
        => ApplyPolicy(migrationBuilder, tableName, BuildOwnOrganizationComparison(organizationIdColumnName));

    /// <summary>
    /// Enables RLS for a content table where a <see langword="null"/> <c>OrganizationId</c> means
    /// "global, shared by every organization" and a non-null value means an org's own/overridden
    /// copy (skills, topics, lessons, exercises, techniques, reference materials, dialog modes —
    /// TENANCY.md §1.2, "Global content library"). The policy is
    /// <c>organization_id IS NULL OR organization_id = current_setting(...)</c> instead of plain
    /// equality, so seeded global content stays visible to every tenant.
    /// </summary>
    public static OperationBuilder<SqlOperation> EnableTenantRlsForContent(
        this MigrationBuilder migrationBuilder,
        string tableName,
        string organizationIdColumnName = DefaultOrganizationIdColumnName)
    {
        var quotedColumn = QuoteIdentifier(organizationIdColumnName);
        var comparison = $"{quotedColumn} IS NULL OR {BuildOwnOrganizationComparison(organizationIdColumnName)}";
        return ApplyPolicy(migrationBuilder, tableName, comparison);
    }

    private static string BuildOwnOrganizationComparison(string organizationIdColumnName)
        => $"{QuoteIdentifier(organizationIdColumnName)} = NULLIF(current_setting('{TenantConnectionInterceptor.OrganizationIdSettingName}', true), '')::uuid";

    private static OperationBuilder<SqlOperation> ApplyPolicy(MigrationBuilder migrationBuilder, string tableName, string comparison)
    {
        var quotedTable = QuoteIdentifier(tableName);
        var quotedPolicy = QuoteIdentifier($"{tableName}_tenant_isolation");

        return migrationBuilder.Sql($"""
            ALTER TABLE {quotedTable} ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {quotedTable} FORCE ROW LEVEL SECURITY;

            CREATE POLICY {quotedPolicy} ON {quotedTable}
                USING ({comparison})
                WITH CHECK ({comparison});
            """);
    }

    private static string QuoteIdentifier(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
