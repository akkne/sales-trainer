using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Ai.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.33. The two tables the meter runs on: what an organization is allowed to spend, and
    /// what it has spent.
    ///
    /// <para>
    /// <b>Strict tenant RLS on both</b>, the same call <c>OrganizationProfileReplicas</c> made in
    /// 40.19 rather than the "mine or global" content policy the dialog library uses. There is no
    /// global allowance and no global bill: a NULL owner on <c>OrganizationQuotas</c> would be one
    /// customer's limit binding everybody, and a NULL owner on <c>AiUsageRecords</c> would be a
    /// month's spend attributed to nobody. The tenant column leads the primary key on both, so a row
    /// without an owner cannot be written at all.
    /// </para>
    ///
    /// <para>
    /// <b>No index beyond the primary keys, and that is a decision.</b> Every read is a prefix scan
    /// of one organization's rows for one month — the leading two key columns — and the whole table
    /// for a busy installation is one row per organization per model per month. That is why
    /// <c>docs/TENANCY/sql/40.33_*_indexes_concurrently.sql</c> does not exist: there is no long
    /// index to build out of band, the same finding 40.19, 40.28, 40.29, 40.31 and 40.32 recorded.
    /// </para>
    ///
    /// <para>
    /// <b>Nothing to backfill.</b> No spend was ever recorded before this migration, and an absent
    /// <c>OrganizationQuotas</c> row deliberately means "the platform defaults in
    /// <c>AiQuotas:Default…</c>" rather than "unmetered" — so every organization is metered from the
    /// first request after the deploy without a single row being written.
    /// </para>
    ///
    /// <para>
    /// The usual trap applies unchanged: the migration/owner role must be a superuser or hold
    /// <c>BYPASSRLS</c>, or <c>FORCE ROW LEVEL SECURITY</c> filters the migration itself
    /// (docs/DECISIONS.md, 2026-08-15).
    /// </para>
    /// </summary>
    public partial class AddOrganizationQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUsageRecords",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PromptTokens = table.Column<long>(type: "bigint", nullable: false),
                    CompletionTokens = table.Column<long>(type: "bigint", nullable: false),
                    CallCount = table.Column<long>(type: "bigint", nullable: false),
                    EstimatedCallCount = table.Column<long>(type: "bigint", nullable: false),
                    SpeechCharacters = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageRecords", x => new { x.OrganizationId, x.PeriodKey, x.Model });
                });

            migrationBuilder.CreateTable(
                name: "OrganizationQuotas",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoiceDailyLimitMinutes = table.Column<int>(type: "integer", nullable: true),
                    VoiceMonthlyLimitMinutes = table.Column<int>(type: "integer", nullable: true),
                    LlmMonthlyTokenLimit = table.Column<long>(type: "bigint", nullable: true),
                    BatchReservePercent = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationQuotas", x => x.OrganizationId);
                });

            migrationBuilder.EnableTenantRls("AiUsageRecords");
            migrationBuilder.EnableTenantRls("OrganizationQuotas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsageRecords");

            migrationBuilder.DropTable(
                name: "OrganizationQuotas");
        }
    }
}
