using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Organization.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Re-applies organization-db's single tenant policy — <c>OrganizationProfiles</c> — so its
    /// <c>USING</c> clause also admits validated platform staff (the owner's role split,
    /// 2026-08-16 — docs/DECISIONS.md, docs/TENANCY/TENANCY.md §1.6). This is the one that matters
    /// most in this database: the tenant registry screen is platform staff's whole job.
    ///
    /// <para>
    /// The <c>Organizations</c> table itself has no policy and gains none — it is the registry of
    /// tenants, not a tenant's data, and scoping it to one organization would make the registry
    /// unreadable by the only people who use it (40.5).
    /// </para>
    ///
    /// <para>The model is untouched; an identical snapshot is expected.</para>
    /// </summary>
    public partial class RefreshTenantPoliciesForPlatformStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
            => migrationBuilder.EnableTenantRls("OrganizationProfiles");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.EnableTenantRls("OrganizationProfiles", admitPlatformStaff: false);
    }
}
