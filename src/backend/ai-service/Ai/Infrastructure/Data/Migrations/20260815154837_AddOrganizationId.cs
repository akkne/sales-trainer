using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Ai.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.11 — stage C continues in ai-db. Only two of the three ai-service tables take an
    /// <c>OrganizationId</c>: <c>DialogBundles</c> and <c>DialogModes</c>, both nullable, because
    /// <c>NULL</c> there means "the global dialog library shared by every organization" — where the
    /// two seeded hidden bundles (<c>company-call</c>, <c>custom-scenario</c>) stay — and a non-null
    /// value means an organization authored the row.
    ///
    /// <para>
    /// <c>UserReplicas</c> deliberately gets nothing, exactly as learning-db's replica did in 40.10:
    /// it is a read-model projection of identity's users, refreshed by a Kafka consumer that runs
    /// with no request and therefore no tenant. Giving it an organization would mean a consumer
    /// deciding a tenant per message, which is 40.13's identity/consumer audit, not this block. Its
    /// only cross-organization reader today is the SuperAdmin voice-usage screen. See
    /// docs/DECISIONS.md (2026-08-15, "ai-service UserReplica stays platform-global in 40.11").
    /// </para>
    ///
    /// <para>
    /// What this migration deliberately does NOT contain: a single <c>CREATE INDEX</c>. The
    /// <c>(OrganizationId, ...)</c> index rebuilds — including the swap of the old
    /// <c>IX_DialogModes_BundleId_Key</c> for the per-organization pair — are an operational step
    /// run by hand with <c>CREATE INDEX CONCURRENTLY</c>
    /// (docs/TENANCY/sql/40.11_ai_organization_indexes_concurrently.sql), because a transactional
    /// index build takes an <c>ACCESS EXCLUSIVE</c> lock and this migration runs from
    /// <c>Database.Migrate()</c> during startup. The EF model snapshot still declares those indexes,
    /// so the snapshot intentionally runs ahead of what the migration creates — the same deliberate
    /// gap 40.10 opened in learning-db, see docs/DECISIONS.md (2026-08-15, "40.10 index rebuilds are
    /// an operational step").
    /// </para>
    ///
    /// <para>
    /// Nor is there a backfill, and none is needed: every existing bundle and mode is global
    /// content, so <c>NULL</c> is already the correct value for all of them. The row that does need
    /// migrating lives in Mongo, not here —
    /// docs/TENANCY/mongo/40.11_dialog_sessions_organization_backfill.js.
    /// </para>
    /// </summary>
    public partial class AddOrganizationId : Migration
    {
        private static readonly string[] ContentTables = ["DialogBundles", "DialogModes"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "DialogModes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "DialogBundles",
                type: "uuid",
                nullable: true);

            // Content policy — "global OR mine", never plain equality, or a new customer would see
            // an empty practice page on day one (docs/TENANCY/TENANCY.md 1.5).
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
                migrationBuilder.DisableTenantRls(tableName);
            }

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "DialogModes");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "DialogBundles");
        }
    }
}
