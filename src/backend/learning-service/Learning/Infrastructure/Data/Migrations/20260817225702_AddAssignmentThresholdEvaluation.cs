using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.22 - completion is a quality threshold, not a click
    /// (docs/TENANCY/ASSIGNMENTS.md 1.1).
    ///
    /// <para>
    /// <b>One table, and the reason it is a table and not a counter.</b> The roadmap's first
    /// completion rule is "3 dialogues scoring at least 70", which is a question about a set of
    /// conversations, not a number that can be incremented. Keeping the set means
    /// AssignmentProgressRecords.AttemptCount and BestScore are recomputed from rows on every
    /// evaluation instead of being counted up - and that is what makes an at-least-once Kafka
    /// redelivery harmless. A counter would drift upward on its own once the Redis dedupe window
    /// expires, and "tried 4 times and did not reach the bar" is the single most consequential line
    /// on the ROP's screen: a number that inflates while nobody practises is worse than no number.
    /// </para>
    ///
    /// <para>
    /// <b>Strict tenant data.</b> A practice conversation happens inside one organization, so
    /// OrganizationId is NOT NULL and the policy is plain equality - the same call 40.17 made for
    /// programmes and 40.21 made for assignments, never the content policy's "IS NULL OR = current".
    /// </para>
    ///
    /// <para>
    /// <b>No concurrent-index script and no backfill, for the third block running.</b> The table is
    /// created empty by this migration, so both indexes are built over zero rows and the ACCESS
    /// EXCLUSIVE lock costs nothing; one of them is the uniqueness that makes reprocessing an event
    /// a no-op, and deferring a correctness constraint to a script somebody has to remember to run
    /// is the worse trade. Nothing can be backfilled either: dialog.evaluated did not carry a grade
    /// before this phase (the ModeKey/QualityScore fields are new), so the history simply does not
    /// exist anywhere to copy from. See docs/DECISIONS.md (2026-08-18) and docs/DONT_FORGET.md.
    /// </para>
    /// </summary>
    public partial class AddAssignmentThresholdEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserDialogScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DialogModeKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DialogModeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDialogScores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDialogScores_OrganizationId_UserId_DialogModeKey_Evalua~",
                table: "UserDialogScores",
                columns: new[] { "OrganizationId", "UserId", "DialogModeKey", "EvaluatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserDialogScores_OrganizationId_UserId_SessionId",
                table: "UserDialogScores",
                columns: new[] { "OrganizationId", "UserId", "SessionId" },
                unique: true);

            // The score is a percentage because every consumer of it compares it against a
            // completion rule's 1-100 bar. ai-service normalizes its own 0-10 grade before
            // publishing; this constraint is what turns "it should be normalized" into a fact the
            // ROP's screen can rely on rather than an assumption about a producer in another
            // service. A session id is likewise never blank: it is half of the uniqueness that makes
            // reprocessing an event a no-op.
            migrationBuilder.Sql("""
                ALTER TABLE "UserDialogScores"
                    ADD CONSTRAINT "CK_UserDialogScores_Score"
                    CHECK ("Score" >= 0 AND "Score" <= 100);

                ALTER TABLE "UserDialogScores"
                    ADD CONSTRAINT "CK_UserDialogScores_SessionId"
                    CHECK (length(btrim("SessionId")) > 0);

                ALTER TABLE "UserDialogScores"
                    ADD CONSTRAINT "CK_UserDialogScores_DialogModeKey"
                    CHECK (length(btrim("DialogModeKey")) > 0);
                """);

            migrationBuilder.EnableTenantRls("UserDialogScores");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("UserDialogScores");

            // Drops the table's indexes and check constraints with it.
            migrationBuilder.DropTable(
                name: "UserDialogScores");
        }
    }
}
