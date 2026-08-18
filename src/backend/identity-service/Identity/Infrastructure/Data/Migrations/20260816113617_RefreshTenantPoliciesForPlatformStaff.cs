using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Identity.Infrastructure.Data.Migrations;

/// <summary>
/// Re-applies identity-db's single tenant policy — <c>Invites</c>, added in 40.7 — so its
/// <c>USING</c> clause also admits validated platform staff (the owner's role split,
/// 2026-08-16 — docs/DECISIONS.md, docs/TENANCY/TENANCY.md §1.6).
///
/// <para>
/// The other organization-aware tables in this database deliberately have no policy and gain
/// none here: <c>Memberships</c> is keyed on the pair and filtered explicitly,
/// <c>OrganizationAuthConfigurations</c> is read before authentication and is cross-tenant by
/// nature, and <c>OrganizationReplicas</c> is a registry projection. Those choices were argued
/// in 40.6/40.8/40.9 and are unchanged by the role split.
/// </para>
///
/// <para>The model is untouched; an identical snapshot is expected.</para>
/// </summary>
public partial class RefreshTenantPoliciesForPlatformStaff : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.EnableTenantRls("Invites");

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.EnableTenantRls("Invites", admitPlatformStaff: false);
}
