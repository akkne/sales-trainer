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
    /// <c>USING</c> and <c>WITH CHECK</c> (the read half additionally admitting validated platform
    /// staff — see <see cref="ApplyPolicy"/>), reading
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
        string organizationIdColumnName = DefaultOrganizationIdColumnName,
        bool admitPlatformStaff = true)
        => ApplyPolicy(
            migrationBuilder,
            tableName,
            BuildOwnOrganizationComparison(organizationIdColumnName),
            admitPlatformStaff);

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
        string organizationIdColumnName = DefaultOrganizationIdColumnName,
        bool admitPlatformStaff = true)
    {
        var quotedColumn = QuoteIdentifier(organizationIdColumnName);
        var comparison = $"{quotedColumn} IS NULL OR {BuildOwnOrganizationComparison(organizationIdColumnName)}";
        return ApplyPolicy(migrationBuilder, tableName, comparison, admitPlatformStaff);
    }

    /// <summary>
    /// Reverses either of the two helpers above, for a migration's <c>Down</c>. Needed from 40.10
    /// on: identity's first RLS table (<c>Invites</c>, 40.7) was created by the same migration that
    /// enabled RLS, so dropping the table was enough; a service that adds <c>OrganizationId</c> to
    /// tables that already existed has to be able to hand them back unprotected. <c>IF EXISTS</c>
    /// keeps the rollback idempotent.
    /// </summary>
    public static OperationBuilder<SqlOperation> DisableTenantRls(
        this MigrationBuilder migrationBuilder,
        string tableName)
    {
        var quotedTable = QuoteIdentifier(tableName);
        var quotedPolicy = QuoteIdentifier($"{tableName}_tenant_isolation");

        return migrationBuilder.Sql($"""
            DROP POLICY IF EXISTS {quotedPolicy} ON {quotedTable};

            ALTER TABLE {quotedTable} NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE {quotedTable} DISABLE ROW LEVEL SECURITY;
            """);
    }

    private static string BuildOwnOrganizationComparison(string organizationIdColumnName)
        => $"{QuoteIdentifier(organizationIdColumnName)} = NULLIF(current_setting('{TenantConnectionInterceptor.OrganizationIdSettingName}', true), '')::uuid";

    /// <summary>
    /// The platform-staff branch of the <c>USING</c> clause (2026-08-16, the owner's role split —
    /// docs/DECISIONS.md). Validated Sellevate staff read across every organization, so the policy
    /// admits every row while <c>app.platform_mode</c> is on.
    ///
    /// <para>
    /// <c>COALESCE(NULLIF(..., ''), 'off')</c> rather than a bare comparison, for the same reason
    /// the organization branch uses <c>NULLIF</c>: once a pooled physical connection has run
    /// <c>SET LOCAL</c> on a GUC even once, Postgres reverts it to <c>''</c> — not NULL — when the
    /// transaction ends. Comparing <c>''</c> to <c>'on'</c> is merely false, so this is defensive
    /// rather than load-bearing, but the two branches reading the same way is what keeps the next
    /// person from "simplifying" the organization branch into the bug it already had once.
    /// </para>
    /// </summary>
    private static string BuildPlatformModeComparison()
        => $"COALESCE(NULLIF(current_setting('{TenantConnectionInterceptor.PlatformModeSettingName}', true), ''), 'off') = 'on'";

    /// <summary>
    /// Emits the policy with an asymmetric pair of clauses: <c>USING</c> (what may be read) carries
    /// the platform-staff branch, <c>WITH CHECK</c> (what may be written) does not. Platform staff
    /// see every organization and can still only write into one they named — the widening is a
    /// visibility privilege, never an authorship one.
    ///
    /// <para>
    /// <c>DROP POLICY IF EXISTS</c> first, so calling the helper again on a table that already has
    /// the policy replaces it instead of failing. That is what lets a later migration re-apply the
    /// current definition rather than hand-writing policy SQL that would drift from this file — and
    /// what lets that migration's <c>Down</c> pass <c>admitPlatformStaff: false</c> to regenerate
    /// the exact pre-2026-08-16 policy through the same code path instead of a copy of it.
    /// </para>
    /// </summary>
    private static OperationBuilder<SqlOperation> ApplyPolicy(
        MigrationBuilder migrationBuilder,
        string tableName,
        string comparison,
        bool admitPlatformStaff)
    {
        var quotedTable = QuoteIdentifier(tableName);
        var quotedPolicy = QuoteIdentifier($"{tableName}_tenant_isolation");
        var readComparison = admitPlatformStaff
            ? $"{BuildPlatformModeComparison()} OR {comparison}"
            : comparison;

        return migrationBuilder.Sql($"""
            ALTER TABLE {quotedTable} ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {quotedTable} FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS {quotedPolicy} ON {quotedTable};

            CREATE POLICY {quotedPolicy} ON {quotedTable}
                USING ({readComparison})
                WITH CHECK ({comparison});
            """);
    }

    private static string QuoteIdentifier(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
