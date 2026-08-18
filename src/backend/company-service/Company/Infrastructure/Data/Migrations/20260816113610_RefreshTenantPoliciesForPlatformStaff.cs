using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Company.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Re-applies every tenant policy in company-db so its <c>USING</c> clause also admits validated
    /// platform staff (the owner's role split, 2026-08-16 — docs/DECISIONS.md,
    /// docs/TENANCY/TENANCY.md §1.6). Same helper as 20260815203733_AddOrganizationId, which now
    /// replaces an existing policy instead of failing on it.
    ///
    /// <para>
    /// Every table here is strict tenant data — a salesperson's prospect list is nobody's shared
    /// library — so there is no content flavour in this database and none appears here.
    /// <c>Down</c> regenerates the exact pre-change policy through the same helper.
    /// </para>
    ///
    /// <para>The model is untouched; an identical snapshot is expected.</para>
    /// </summary>
    public partial class RefreshTenantPoliciesForPlatformStaff : Migration
    {
        private static readonly string[] TenantScopedTables =
        [
            "Companies",
            "CallLogEntries",
            "PracticeCalls",
            "CompanyContacts",
            "CompanyPersonas"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var tableName in TenantScopedTables)
            {
                migrationBuilder.EnableTenantRls(tableName);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tableName in TenantScopedTables)
            {
                migrationBuilder.EnableTenantRls(tableName, admitPlatformStaff: false);
            }
        }
    }
}
