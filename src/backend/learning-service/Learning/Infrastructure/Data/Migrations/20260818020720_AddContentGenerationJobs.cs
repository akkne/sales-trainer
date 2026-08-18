using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.27 - the checkpoint between structuring and generation (roadmap 40.27).
    ///
    /// <para>
    /// <b>The checkpoint is a database constraint, not only a service rule.</b>
    /// CK_ContentGenerationJobs_Checkpoint says that a run may not be in the generating state
    /// without a structure and an ApprovedAt. That is the whole block stated once, in the place a
    /// second writer added later cannot forget it: no lesson is ever generated from a structure no
    /// human confirmed. The service enforces the same thing, and would be the only thing enforcing
    /// it if this constraint were left out.
    /// </para>
    ///
    /// <para>
    /// <b>Strict tenant data.</b> A run holds one customer's uploaded product deck and the
    /// objections, script and compliance list read out of it, so OrganizationId is NOT NULL and the
    /// policy is plain equality - never the content policy's "IS NULL OR = current", which on this
    /// table would publish one customer's material to every other. The lesson the run produces is
    /// content and is filtered by the content rule, but it is always owned and never global: the
    /// shared library has exactly one authoring path and it is the seeder (docs/SEEDER.md 0).
    /// </para>
    ///
    /// <para>
    /// <b>No backfill, no maintenance window, no concurrent-index script</b> - the sixth block in a
    /// row to make that call, for the same reason: the table is created empty here, so all three
    /// indexes are built over zero rows and the ACCESS EXCLUSIVE lock costs nothing. There is
    /// nothing to backfill either - no pipeline run has ever existed anywhere to copy from.
    /// </para>
    ///
    /// <para>
    /// <b>ProducedLessonId is not a foreign key, deliberately.</b> Lessons are a content table under
    /// an "IS NULL OR = current" policy and this is strict tenant data under plain equality; 40.16
    /// already refused to join those two with a constraint validated with the writer's privileges,
    /// and the same argument holds here. What makes the value trustworthy is that only this service
    /// writes it, in the same transaction that creates the lesson.
    /// </para>
    /// </summary>
    public partial class AddContentGenerationJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentGenerationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceMaterial = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "structuring"),
                    Structure = table.Column<string>(type: "jsonb", nullable: true),
                    StructuredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ProducedLessonId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProducedLessonVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProducedExerciseCount = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentGenerationJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentGenerationJobs_OrganizationId_CreatedAt",
                table: "ContentGenerationJobs",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentGenerationJobs_OrganizationId_ProducedLessonId",
                table: "ContentGenerationJobs",
                columns: new[] { "OrganizationId", "ProducedLessonId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentGenerationJobs_OrganizationId_Status_CreatedAt",
                table: "ContentGenerationJobs",
                columns: new[] { "OrganizationId", "Status", "CreatedAt" });

            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    ADD CONSTRAINT "CK_ContentGenerationJobs_Status"
                    CHECK ("Status" IN ('structuring', 'awaiting_review', 'generating', 'completed', 'failed'));
                """);

            // The block, in one constraint. Generation may not start without a structure and without
            // a human having said the structure was right - which is the entire difference between
            // this pipeline and the one that goes from a deck to fifteen exercises in a single hop.
            // Stated here as well as in the service so that a second writer added later inherits the
            // rule rather than having to remember it.
            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    ADD CONSTRAINT "CK_ContentGenerationJobs_Checkpoint"
                    CHECK (
                        "Status" <> 'generating'
                        OR ("Structure" IS NOT NULL AND "ApprovedAt" IS NOT NULL)
                    );
                """);

            // A run at the checkpoint has something to review, and an approval names a structure. The
            // second half is what stops an approval from being recorded against a run whose
            // structuring call never returned.
            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    ADD CONSTRAINT "CK_ContentGenerationJobs_Structure"
                    CHECK (
                        ("Status" <> 'awaiting_review' OR "Structure" IS NOT NULL)
                        AND ("ApprovedAt" IS NULL OR "Structure" IS NOT NULL)
                    );
                """);

            // A produced lesson is a fact about a finished, approved run. Without this a row could
            // claim a lesson while sitting at the checkpoint, and the cost guard that reads
            // ProducedLessonId would then refuse to generate something that was never generated.
            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    ADD CONSTRAINT "CK_ContentGenerationJobs_Produced"
                    CHECK (
                        "ProducedLessonId" IS NULL
                        OR ("Status" = 'completed' AND "ApprovedAt" IS NOT NULL AND "GeneratedAt" IS NOT NULL)
                    );
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    ADD CONSTRAINT "CK_ContentGenerationJobs_Counters"
                    CHECK ("Attempts" >= 0 AND "ProducedExerciseCount" >= 0);
                """);

            // An empty title or an empty material is a run that can only ever fail, and it would fail
            // after paying for a call.
            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    ADD CONSTRAINT "CK_ContentGenerationJobs_Input"
                    CHECK (length(btrim("Title")) > 0 AND length(btrim("SourceMaterial")) > 0);
                """);

            migrationBuilder.EnableTenantRls("ContentGenerationJobs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("ContentGenerationJobs");

            // Drops the table's indexes and check constraints with it.
            migrationBuilder.DropTable(
                name: "ContentGenerationJobs");
        }
    }
}
