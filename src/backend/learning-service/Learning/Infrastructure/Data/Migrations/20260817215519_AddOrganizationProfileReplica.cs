using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.19. The local read-only copy of organization-service's content-substitution profile
    /// (docs/TENANCY/CONTENT_MODEL.md §3), projected by <c>OrganizationProfileConsumer</c>.
    ///
    /// <para>
    /// <b>Strict tenant RLS, not content RLS.</b> Every other table this migration folder has added
    /// since 40.15 is content, where a NULL owner means "the shared library". Here a NULL owner would
    /// mean "every organization's product name and banned claims at once", which is the opposite of
    /// what a profile is. Plain equality, and the column is the primary key so there is no way to
    /// write a row without one.
    /// </para>
    ///
    /// <para>
    /// No backfill and no index beyond the primary key. There is nothing to backfill — the profile
    /// is owned elsewhere and arrives by event — and the only query against this table is a lookup by
    /// its own primary key, so <c>docs/TENANCY/sql/40.19_*.sql</c> contains no CONCURRENTLY script by
    /// design rather than by omission.
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
            // Dropping the table takes its policy with it, so DisableTenantRls would be redundant
            // here — unlike 40.10-40.13, which added columns to tables that already existed.
            migrationBuilder.DropTable(
                name: "OrganizationProfileReplicas");
        }
    }
}
