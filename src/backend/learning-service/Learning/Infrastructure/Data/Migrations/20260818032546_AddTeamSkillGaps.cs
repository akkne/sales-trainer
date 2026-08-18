using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.31 - closing the loop from metric to content (roadmap 40.31).
    ///
    /// <para>
    /// <b>Only the refusals are stored, and that is the block's central decision.</b> The suggestions
    /// themselves are computed on every read from the same heat map the screen draws, so a gap that
    /// closes stops being offered without anything having to notice it - the shape 40.18 used for
    /// staleness and 40.25 for the funnel. A table of proposed gaps would need a writer, an expiry
    /// sweep and a rule for extinguishing a row whose number has recovered, all to hold a fact the
    /// matrix already answers. What genuinely cannot be derived is that a person said "no", which is
    /// why TeamSkillGapDismissals exists and why it is the only table here.
    /// </para>
    ///
    /// <para>
    /// <b>Strict tenant data, plain equality.</b> A refusal is one РОП's decision about their own
    /// team's panel. A null owner would silence one organization's suggestion for every other, which
    /// is the loudest possible version of the leak this whole phase exists to prevent.
    /// </para>
    ///
    /// <para>
    /// <b>ContentGenerationJobs.GapSourceRef is provenance and deduplication in one column.</b> An
    /// assignment created from such a run copies it into Assignments.SourceRef and takes
    /// 'gap_detected' as its source type, so no HTTP caller can claim a provenance it did not earn;
    /// and the presence of a live run holding a stage's reference is what stops the panel offering
    /// the same stage again next week. CK_ContentGenerationJobs_GapSourceRef keeps the string inside
    /// the one namespace that can be parsed back to a stage - a provenance nobody can read is worse
    /// than none, because the panel would then neither suppress on it nor be able to say why.
    /// </para>
    ///
    /// <para>
    /// <b>No backfill, no maintenance window, no concurrent-index script.</b> The seventh block in a
    /// row to make that call, and for the same reason: TeamSkillGapDismissals is created empty here,
    /// so its unique index is built over zero rows, and the partial index on GapSourceRef covers a
    /// column that has just been added and is NULL on every existing row. The ADD COLUMN itself is
    /// metadata-only in Postgres 11+ (nullable, no default), so nothing rewrites the table. Hence
    /// there is no docs/TENANCY/sql/40.31_*_indexes_concurrently.sql - there is no long index to
    /// build. The read-only check script is docs/TENANCY/sql/40.31_skill_gaps_verify.sql.
    /// </para>
    /// </summary>
    public partial class AddTeamSkillGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GapSourceRef",
                table: "ContentGenerationJobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TeamSkillGapDismissals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DismissedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AccuracyPercentAtDismissal = table.Column<int>(type: "integer", nullable: false),
                    AttemptCountAtDismissal = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamSkillGapDismissals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentGenerationJobs_OrganizationId_GapSourceRef",
                table: "ContentGenerationJobs",
                columns: new[] { "OrganizationId", "GapSourceRef" },
                filter: "\"GapSourceRef\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeamSkillGapDismissals_OrganizationId_StageKey",
                table: "TeamSkillGapDismissals",
                columns: new[] { "OrganizationId", "StageKey" },
                unique: true);

            // The namespace, in the database. SkillGapSourceRefs builds and parses these strings, and
            // it would be the only thing enforcing the shape if this constraint were left out - the
            // same call 40.27 made for its checkpoint and 40.21 for its source columns: the invariant
            // that defines a block belongs where the next writer inherits it instead of having to
            // remember it.
            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    ADD CONSTRAINT "CK_ContentGenerationJobs_GapSourceRef"
                    CHECK ("GapSourceRef" IS NULL OR "GapSourceRef" LIKE 'skill-gap:%@%');
                """);

            // A refusal that never runs out would quietly turn the panel off for the one stage most
            // in need of it, and a refusal recorded against no number could not be overruled by the
            // number getting worse. Both are what make a dismissal a pause rather than a mute.
            migrationBuilder.Sql("""
                ALTER TABLE "TeamSkillGapDismissals"
                    ADD CONSTRAINT "CK_TeamSkillGapDismissals_Window"
                    CHECK ("ExpiresAt" > "DismissedAt");

                ALTER TABLE "TeamSkillGapDismissals"
                    ADD CONSTRAINT "CK_TeamSkillGapDismissals_Measurement"
                    CHECK (
                        "AccuracyPercentAtDismissal" BETWEEN 0 AND 100
                        AND "AttemptCountAtDismissal" >= 0
                    );

                ALTER TABLE "TeamSkillGapDismissals"
                    ADD CONSTRAINT "CK_TeamSkillGapDismissals_StageKey"
                    CHECK (
                        length(btrim("StageKey")) > 0
                        AND "StageKey" NOT LIKE '%:%'
                        AND "StageKey" NOT LIKE '%@%'
                    );
                """);

            migrationBuilder.EnableTenantRls("TeamSkillGapDismissals");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("TeamSkillGapDismissals");

            // Drops the table's index and check constraints with it.
            migrationBuilder.DropTable(
                name: "TeamSkillGapDismissals");

            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    DROP CONSTRAINT "CK_ContentGenerationJobs_GapSourceRef";
                """);

            migrationBuilder.DropIndex(
                name: "IX_ContentGenerationJobs_OrganizationId_GapSourceRef",
                table: "ContentGenerationJobs");

            migrationBuilder.DropColumn(
                name: "GapSourceRef",
                table: "ContentGenerationJobs");
        }
    }
}
