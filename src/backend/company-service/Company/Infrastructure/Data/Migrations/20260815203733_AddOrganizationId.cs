using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Company.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.12 — Stage C reaches company-db.
    ///
    /// <para>
    /// Every table in this database is user data: a salesperson's own prospect list, their contacts,
    /// their personas, their practice calls and their real-call log. There is no global content
    /// library here, so every policy is the <b>strict</b> flavour (<c>EnableTenantRls</c>, plain
    /// equality) rather than the content flavour learning-service and ai-service needed. A row with
    /// no organization must be invisible, not shared.
    /// </para>
    ///
    /// <para>
    /// The column lands with an all-zeros placeholder default so the DDL does not have to rewrite
    /// every row twice. The real organization is written by
    /// <c>docs/TENANCY/sql/40.12_company_organization_backfill.sql</c>, which is an operational step
    /// and deliberately not part of this migration.
    /// </para>
    ///
    /// <para>
    /// This migration deliberately contains <b>no</b> index work at all — it neither creates the new
    /// <c>(OrganizationId, ...)</c> indexes nor drops the ones they supersede. Both happen in
    /// <c>docs/TENANCY/sql/40.12_company_organization_indexes_concurrently.sql</c> with
    /// <c>CREATE INDEX CONCURRENTLY</c>, and the old indexes are dropped only after the replacements
    /// are verified valid — so the tables are never left without an index for the foreign-key
    /// cascade or the timeline read. The EF model snapshot therefore describes the post-rollout
    /// index set, which the migration does not produce; that divergence is intentional and matches
    /// 40.10/40.11.
    /// </para>
    /// </summary>
    public partial class AddOrganizationId : Migration
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
                migrationBuilder.AddColumn<Guid>(
                    name: "OrganizationId",
                    table: tableName,
                    type: "uuid",
                    nullable: false,
                    defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            }

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
                migrationBuilder.DisableTenantRls(tableName);
            }

            foreach (var tableName in TenantScopedTables)
            {
                migrationBuilder.DropColumn(
                    name: "OrganizationId",
                    table: tableName);
            }
        }
    }
}
