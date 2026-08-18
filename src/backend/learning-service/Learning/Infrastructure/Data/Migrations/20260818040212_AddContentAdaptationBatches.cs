using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.32 - batch tone adaptation and AI content review (roadmap 40.32).
    ///
    /// <para>
    /// <b>Two tables because the money is spent per exercise.</b> 40.27 could keep one row per run,
    /// since a run makes two calls. A batch makes up to sixty, and "which of them have we already paid
    /// for" is the question that decides what an interrupted batch costs. So the item is the row: it
    /// carries the proposal, the attempt count and the fingerprint of the body the model was shown,
    /// and an item that carries an answer is never queued again.
    /// </para>
    ///
    /// <para>
    /// <b>CK_ContentAdaptationItems_Proposal is this block's checkpoint constraint</b>, the way
    /// CK_ContentGenerationJobs_Checkpoint was 40.27's. It says an accepted item must carry both a
    /// proposal and a record of where that proposal was written, and that nothing outside the accepted
    /// state may carry an application. What the database cannot say is "a human pressed it" - that is
    /// enforced by shape instead: the worker has no branch that writes 'accepted' or 'rejected', and
    /// the only code that writes an Exercise is an admin request handler.
    /// </para>
    ///
    /// <para>
    /// <b>UX_ContentAdaptationJobs_Live is a cost control, not a tidiness rule.</b> Two clicks a
    /// second apart both read no live batch under READ COMMITTED and both start one, and the customer
    /// pays twice for the same sixty rewrites. Partial over the two live statuses so that a finished
    /// batch never blocks the next one.
    /// </para>
    ///
    /// <para>
    /// <b>Strict tenant data on both tables, plain equality.</b> A batch names one customer's stage and
    /// its items hold that customer's exercises rewritten in their voice; a null owner would publish
    /// them to every other organization. The item's owner is additionally tied to its batch's by a
    /// composite foreign key rather than by convention - the only way to state "an item belongs to the
    /// organization its batch belongs to" where the next writer inherits it.
    /// </para>
    ///
    /// <para>
    /// <b>No backfill, no maintenance window, no concurrent-index script.</b> Both tables are created
    /// empty here, so every index is built over zero rows and there is nothing to rewrite. Hence there
    /// is no docs/TENANCY/sql/40.32_*_indexes_concurrently.sql - there is no long index to build. The
    /// read-only check script is docs/TENANCY/sql/40.32_content_adaptation_verify.sql.
    /// </para>
    /// </summary>
    public partial class AddContentAdaptationBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentAdaptationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "tone_rewrite"),
                    StageKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "preparing"),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentAdaptationJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentAdaptationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ExerciseType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderInLesson = table.Column<int>(type: "integer", nullable: false),
                    BaseContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    ProposedContent = table.Column<string>(type: "jsonb", nullable: true),
                    ChangeSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedFieldCount = table.Column<int>(type: "integer", nullable: false),
                    Findings = table.Column<string>(type: "jsonb", nullable: true),
                    AppliedExerciseId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentAdaptationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentAdaptationItems_ContentAdaptationJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "ContentAdaptationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentAdaptationItems_JobId",
                table: "ContentAdaptationItems",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentAdaptationItems_OrganizationId_ExerciseId_Status",
                table: "ContentAdaptationItems",
                columns: new[] { "OrganizationId", "ExerciseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentAdaptationItems_OrganizationId_JobId_LessonId_OrderI~",
                table: "ContentAdaptationItems",
                columns: new[] { "OrganizationId", "JobId", "LessonId", "OrderInLesson" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentAdaptationItems_OrganizationId_JobId_Status",
                table: "ContentAdaptationItems",
                columns: new[] { "OrganizationId", "JobId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentAdaptationJobs_OrganizationId_CreatedAt",
                table: "ContentAdaptationJobs",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentAdaptationJobs_OrganizationId_Status_CreatedAt",
                table: "ContentAdaptationJobs",
                columns: new[] { "OrganizationId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_ContentAdaptationJobs_Live",
                table: "ContentAdaptationJobs",
                columns: new[] { "OrganizationId", "Mode", "StageKey" },
                unique: true,
                filter: "\"Status\" IN ('preparing', 'awaiting_review')");

            // The vocabularies, in the database. ContentAdaptationModes / ContentAdaptationStatuses /
            // ContentAdaptationItemStatuses would otherwise be the only thing enforcing them - the same
            // call 40.27 made for its checkpoint, 40.21 for its source columns and 40.31 for its source
            // ref: the invariant that defines a block belongs where the next writer inherits it instead
            // of having to remember it.
            migrationBuilder.Sql("""
                ALTER TABLE "ContentAdaptationJobs"
                    ADD CONSTRAINT "CK_ContentAdaptationJobs_Mode"
                    CHECK ("Mode" IN ('tone_rewrite', 'quality_review'));

                ALTER TABLE "ContentAdaptationJobs"
                    ADD CONSTRAINT "CK_ContentAdaptationJobs_Status"
                    CHECK ("Status" IN ('preparing', 'awaiting_review', 'completed', 'failed'));

                ALTER TABLE "ContentAdaptationJobs"
                    ADD CONSTRAINT "CK_ContentAdaptationJobs_StageKey"
                    CHECK (length(btrim("StageKey")) > 0);

                ALTER TABLE "ContentAdaptationJobs"
                    ADD CONSTRAINT "CK_ContentAdaptationJobs_ItemCount"
                    CHECK ("ItemCount" >= 0);
                """);

            // "Finished" and "has a finish time" are the same fact, so the table refuses to hold one
            // without the other. A batch that reads as completed with no completion time is the kind of
            // row that makes an admin list sort wrongly and nobody can say since when.
            migrationBuilder.Sql("""
                ALTER TABLE "ContentAdaptationJobs"
                    ADD CONSTRAINT "CK_ContentAdaptationJobs_Completed"
                    CHECK (("Status" IN ('completed', 'failed')) = ("CompletedAt" IS NOT NULL));
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "ContentAdaptationItems"
                    ADD CONSTRAINT "CK_ContentAdaptationItems_Status"
                    CHECK ("Status" IN ('pending', 'proposed', 'unchanged', 'accepted', 'rejected', 'failed'));

                ALTER TABLE "ContentAdaptationItems"
                    ADD CONSTRAINT "CK_ContentAdaptationItems_Counters"
                    CHECK ("Attempts" >= 0 AND "ChangedFieldCount" >= 0 AND length("BaseContentHash") = 64);
                """);

            // The block's central invariant, as a constraint. An accepted item must carry the proposal
            // it applied and the row it was applied to; nothing outside the accepted state may claim an
            // application; and an item cannot sit in the review queue with nothing for a person to look
            // at. Together these make "auto-applied without a proposal" and "applied without being
            // accepted" unrepresentable rather than merely unwritten.
            migrationBuilder.Sql("""
                ALTER TABLE "ContentAdaptationItems"
                    ADD CONSTRAINT "CK_ContentAdaptationItems_Proposal"
                    CHECK (
                        ("Status" <> 'accepted' OR (
                            "ProposedContent" IS NOT NULL
                            AND "AppliedAt" IS NOT NULL
                            AND "AppliedExerciseId" IS NOT NULL))
                        AND ("AppliedAt" IS NULL OR "Status" = 'accepted')
                        AND ("Status" <> 'proposed' OR "ProposedContent" IS NOT NULL OR "Findings" IS NOT NULL)
                    );

                ALTER TABLE "ContentAdaptationItems"
                    ADD CONSTRAINT "CK_ContentAdaptationItems_Resolution"
                    CHECK (("Status" IN ('accepted', 'rejected')) = ("ResolvedAt" IS NOT NULL));
                """);

            // "An item belongs to the organization its batch belongs to", stated where it cannot be
            // forgotten. The plain FK on JobId alone would let a row name one organization while its
            // batch names another - invisible to RLS, which checks each row against the session tenant
            // and never against its parent.
            migrationBuilder.Sql("""
                ALTER TABLE "ContentAdaptationJobs"
                    ADD CONSTRAINT "UQ_ContentAdaptationJobs_Id_OrganizationId"
                    UNIQUE ("Id", "OrganizationId");

                ALTER TABLE "ContentAdaptationItems"
                    ADD CONSTRAINT "FK_ContentAdaptationItems_Job_Organization"
                    FOREIGN KEY ("JobId", "OrganizationId")
                    REFERENCES "ContentAdaptationJobs" ("Id", "OrganizationId")
                    ON DELETE CASCADE;
                """);

            migrationBuilder.EnableTenantRls("ContentAdaptationJobs");
            migrationBuilder.EnableTenantRls("ContentAdaptationItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("ContentAdaptationItems");
            migrationBuilder.DisableTenantRls("ContentAdaptationJobs");

            // Drops each table's indexes, check constraints and foreign keys with it.
            migrationBuilder.DropTable(
                name: "ContentAdaptationItems");

            migrationBuilder.DropTable(
                name: "ContentAdaptationJobs");
        }
    }
}
