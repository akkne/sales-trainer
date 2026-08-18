using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Ai.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.19. The local read-only copy of organization-service's content-substitution profile
    /// (docs/TENANCY/CONTENT_MODEL.md §3), projected by <c>OrganizationProfileConsumer</c> and read
    /// when a persona prompt is built.
    ///
    /// <para>
    /// <b>Strict tenant RLS, and this is the first non-content table in ai-db.</b> Both existing
    /// tables here are content, where a NULL owner means "the global dialog library". A NULL owner
    /// on this table would mean one organization's <c>banned_claims</c> binding everybody's calls —
    /// so plain equality, with the tenant column as the primary key so a row without an owner cannot
    /// be written at all.
    /// </para>
    ///
    /// <para>
    /// Nothing to backfill and nothing to index beyond the primary key: the profile is owned
    /// elsewhere and arrives by event, and the only query is a lookup by that key. That is why
    /// <c>docs/TENANCY/sql/</c> has no <c>40.19_*_indexes_concurrently.sql</c> — a decision, not an
    /// omission.
    /// </para>
    /// </summary>
    public partial class AddOrganizationProfileReplica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationProfileReplicas",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Product = table.Column<string>(type: "text", nullable: true),
                    Icp = table.Column<string>(type: "text", nullable: true),
                    Tone = table.Column<string>(type: "text", nullable: true),
                    ObjectionsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    ScriptJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    GlossaryJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    BannedClaimsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationProfileReplicas", x => x.OrganizationId);
                });

            migrationBuilder.EnableTenantRls("OrganizationProfileReplicas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationProfileReplicas");
        }
    }
}
