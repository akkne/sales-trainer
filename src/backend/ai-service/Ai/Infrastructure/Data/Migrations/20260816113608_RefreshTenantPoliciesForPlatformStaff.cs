using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Ai.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Re-applies ai-db's two content policies so their <c>USING</c> clause also admits validated
    /// platform staff (the owner's role split, 2026-08-16 — docs/DECISIONS.md,
    /// docs/TENANCY/TENANCY.md §1.6). Same helpers as 20260815154837_AddOrganizationId, which now
    /// replace an existing policy instead of failing on it, so the policy text has exactly one
    /// definition and it is not in this file.
    ///
    /// <para>
    /// Mongo's <c>dialog_sessions</c> is not affected here and cannot be: it has no row-level
    /// security, so its equivalent widening lives in <c>DialogSessionRepository.TenantFilter</c>.
    /// </para>
    ///
    /// <para>The model is untouched; an identical snapshot is expected.</para>
    /// </summary>
    public partial class RefreshTenantPoliciesForPlatformStaff : Migration
    {
        private static readonly string[] ContentTables = ["DialogBundles", "DialogModes"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var tableName in ContentTables)
            {
                migrationBuilder.EnableTenantRlsForContent(tableName);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tableName in ContentTables)
            {
                migrationBuilder.EnableTenantRlsForContent(tableName, admitPlatformStaff: false);
            }
        }
    }
}
