using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.21 - the Assignment entity (docs/TENANCY/ASSIGNMENTS.md 1). Stage E of the tenancy
    /// roadmap opens here: Stage D gave content a history and a per-organization curriculum, and this
    /// gives the РОП a way to ask their team for something short, specific and dated.
    ///
    /// <para>
    /// <b>Two tables, both strict tenant data.</b> Like the programme tables of 40.17 and unlike
    /// everything else Stage D added to learning-db, there is no global flavour: an assignment is a
    /// decision one organization made about its own people, so OrganizationId is NOT NULL and both
    /// policies are plain equality rather than "IS NULL OR = current". A NULL owner here would mean
    /// "everybody's homework", which is not a thing.
    /// </para>
    ///
    /// <para>
    /// <b>Why the indexes are in the migration and there is no 40.21_*_indexes_concurrently.sql.</b>
    /// The same call 40.15 and 40.17 made, for the same reason: both tables are created empty by this
    /// very migration, so every index is built over zero rows and the ACCESS EXCLUSIVE lock a
    /// transactional build takes costs nothing. 40.10-40.13 needed concurrent scripts because they
    /// were rebuilding indexes on tables that were already large and already live; nothing here has
    /// that shape. One of the indexes is a correctness constraint (one progress row per person per
    /// assignment), and deferring a correctness constraint to a script somebody has to remember to run
    /// is the worse trade.
    /// </para>
    ///
    /// <para>
    /// <b>And no backfill, because there is nothing to backfill.</b> No assignment exists before this
    /// migration and nothing derives one from the skill tree - an assignment is not a snapshot of
    /// something that already exists but a request somebody has yet to make. Both tables start empty
    /// and stay empty until a РОП creates something; AssignmentProgressRecords additionally has no
    /// writer at all until 40.23 resolves an audience. See docs/DECISIONS.md (2026-08-18) and
    /// docs/DONT_FORGET.md.
    /// </para>
    /// </summary>
    public partial class AddAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Goal = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SourceType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    Audience = table.Column<string>(type: "jsonb", nullable: false),
                    OpensAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletionRule = table.Column<string>(type: "jsonb", nullable: false),
                    RepeatSchedule = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "draft"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentProgressRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "not_started"),
                    BestScore = table.Column<int>(type: "integer", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    FirstOpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentProgressRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentProgressRecords_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentProgressRecords_AssignmentId_Status",
                table: "AssignmentProgressRecords",
                columns: new[] { "AssignmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentProgressRecords_OrganizationId_AssignmentId_UserId",
                table: "AssignmentProgressRecords",
                columns: new[] { "OrganizationId", "AssignmentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentProgressRecords_OrganizationId_UserId_Status",
                table: "AssignmentProgressRecords",
                columns: new[] { "OrganizationId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_OrganizationId_CreatedAt",
                table: "Assignments",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_OrganizationId_Status_Deadline",
                table: "Assignments",
                columns: new[] { "OrganizationId", "Status", "Deadline" });

            // Invariants the model cannot state. Two of them are worth more than the rest.
            //
            // CK_Assignments_CompletionRule refuses a rule that is not an object naming its kind. The
            // column has no default and the API has no way to omit it, so "completion means opening
            // everything" - the compliance-theatre failure ASSIGNMENTS.md 1.1 is written to prevent -
            // has no resting place in the schema either. The vocabulary of kinds stays 40.22's to
            // define; requiring that there IS one is the most that can be asserted without inventing
            // it.
            //
            // CK_Assignments_ManualHasNoSourceRef ties the two source columns together, because the
            // second is read according to the first: a manual assignment with a dangling source
            // reference is a row nobody can interpret a year later.
            migrationBuilder.Sql("""
                ALTER TABLE "Assignments"
                    ADD CONSTRAINT "CK_Assignments_Status"
                    CHECK ("Status" IN ('draft', 'active', 'closed'));

                ALTER TABLE "Assignments"
                    ADD CONSTRAINT "CK_Assignments_SourceType"
                    CHECK ("SourceType" IN ('training', 'manual', 'gap_detected'));

                ALTER TABLE "Assignments"
                    ADD CONSTRAINT "CK_Assignments_ManualHasNoSourceRef"
                    CHECK ("SourceType" <> 'manual' OR "SourceRef" IS NULL);

                ALTER TABLE "Assignments"
                    ADD CONSTRAINT "CK_Assignments_Schedule"
                    CHECK ("Deadline" IS NULL OR "OpensAt" IS NULL OR "Deadline" > "OpensAt");

                ALTER TABLE "Assignments"
                    ADD CONSTRAINT "CK_Assignments_ActivatedAt"
                    CHECK ("Status" = 'draft' OR "ActivatedAt" IS NOT NULL);

                ALTER TABLE "Assignments"
                    ADD CONSTRAINT "CK_Assignments_ClosedAt"
                    CHECK ("Status" <> 'closed' OR "ClosedAt" IS NOT NULL);

                ALTER TABLE "Assignments"
                    ADD CONSTRAINT "CK_Assignments_Content"
                    CHECK (jsonb_typeof("Content") = 'object');

                ALTER TABLE "Assignments"
                    ADD CONSTRAINT "CK_Assignments_Audience"
                    CHECK (jsonb_typeof("Audience") = 'object' AND jsonb_exists("Audience", 'kind'));

                ALTER TABLE "Assignments"
                    ADD CONSTRAINT "CK_Assignments_CompletionRule"
                    CHECK (jsonb_typeof("CompletionRule") = 'object' AND jsonb_exists("CompletionRule", 'kind'));

                ALTER TABLE "Assignments"
                    ADD CONSTRAINT "CK_Assignments_RepeatSchedule"
                    CHECK ("RepeatSchedule" IS NULL
                           OR (jsonb_typeof("RepeatSchedule") = 'object' AND jsonb_exists("RepeatSchedule", 'kind')));
                """);

            // The progress vocabulary, and the three facts that make failed_threshold readable: a
            // score is a percentage, an attempt count is not negative, and a row that claims a
            // terminal state has to carry the timestamps that state implies. Without the last one,
            // "completed" rows with no completion time turn 40.25's funnel into a report nobody can
            // reconcile.
            migrationBuilder.Sql("""
                ALTER TABLE "AssignmentProgressRecords"
                    ADD CONSTRAINT "CK_AssignmentProgressRecords_Status"
                    CHECK ("Status" IN ('not_started', 'in_progress', 'completed', 'failed_threshold'));

                ALTER TABLE "AssignmentProgressRecords"
                    ADD CONSTRAINT "CK_AssignmentProgressRecords_BestScore"
                    CHECK ("BestScore" IS NULL OR ("BestScore" >= 0 AND "BestScore" <= 100));

                ALTER TABLE "AssignmentProgressRecords"
                    ADD CONSTRAINT "CK_AssignmentProgressRecords_AttemptCount"
                    CHECK ("AttemptCount" >= 0);

                ALTER TABLE "AssignmentProgressRecords"
                    ADD CONSTRAINT "CK_AssignmentProgressRecords_CompletedAt"
                    CHECK ("Status" <> 'completed' OR "CompletedAt" IS NOT NULL);

                ALTER TABLE "AssignmentProgressRecords"
                    ADD CONSTRAINT "CK_AssignmentProgressRecords_FirstOpenedAt"
                    CHECK ("Status" = 'not_started' OR "FirstOpenedAt" IS NOT NULL);
                """);

            // Strict tenant isolation on both: an assignment and one person's standing on it each
            // belong to exactly one organization (docs/TENANCY/TENANCY.md 1.5).
            migrationBuilder.EnableTenantRls("Assignments");
            migrationBuilder.EnableTenantRls("AssignmentProgressRecords");

            // "What an issued assignment asked for is what its scores describe" is the property this
            // trigger exists for, and it is the assignment's version of the freeze 40.15 put on lesson
            // versions and 40.17 put on programme versions.
            //
            // The frozen set is deliberately narrow: SourceType, SourceRef, Content and CompletionRule
            // are what every recorded attempt was measured against, so moving them retroactively makes
            // every stored BestScore describe something that no longer exists. Title, Goal, Audience,
            // OpensAt, Deadline and RepeatSchedule stay writable on purpose - adding three people to a
            // running assignment and extending a deadline are ordinary acts of running a team, and a
            // trigger that forbade them would be one 40.23 and 40.24 have to break.
            //
            // A closed assignment is frozen whole. It is history at that point, and there is no
            // reopening path by design: the answer to "we want that practice again" is a new
            // assignment, which is also exactly what 40.24's repeats will create.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION "assignment_reject_frozen_change"()
                RETURNS trigger AS $BODY$
                BEGIN
                    IF OLD."Status" = 'closed' THEN
                        IF NEW IS DISTINCT FROM OLD THEN
                            RAISE EXCEPTION
                                'Assignment % is closed, and a closed assignment is a record of what was '
                                'asked and what happened. Create a new assignment instead.',
                                OLD."Id";
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF OLD."Status" = 'draft' THEN
                        IF NEW."Status" NOT IN ('draft', 'active') THEN
                            RAISE EXCEPTION
                                'Assignment % is a draft and can only be issued, not moved to %.',
                                OLD."Id", NEW."Status";
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF NEW."OrganizationId" IS DISTINCT FROM OLD."OrganizationId"
                        OR NEW."SourceType" IS DISTINCT FROM OLD."SourceType"
                        OR NEW."SourceRef" IS DISTINCT FROM OLD."SourceRef"
                        OR NEW."Content" IS DISTINCT FROM OLD."Content"
                        OR NEW."CompletionRule" IS DISTINCT FROM OLD."CompletionRule"
                        OR NEW."ActivatedAt" IS DISTINCT FROM OLD."ActivatedAt"
                    THEN
                        RAISE EXCEPTION
                            'Assignment % has been issued, so what it asks for and what counts as done are '
                            'frozen. Every score already recorded was measured against them. Close it and '
                            'create a new assignment instead.',
                            OLD."Id";
                    END IF;

                    IF NEW."Status" NOT IN ('active', 'closed') THEN
                        RAISE EXCEPTION
                            'Assignment % cannot go from active back to %.', OLD."Id", NEW."Status";
                    END IF;

                    RETURN NEW;
                END;
                $BODY$ LANGUAGE plpgsql;

                CREATE TRIGGER "Assignments_reject_frozen_change"
                    BEFORE UPDATE ON "Assignments"
                    FOR EACH ROW
                    EXECUTE FUNCTION "assignment_reject_frozen_change"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "Assignments_reject_frozen_change" ON "Assignments";
                DROP FUNCTION IF EXISTS "assignment_reject_frozen_change"();
                """);

            migrationBuilder.DisableTenantRls("AssignmentProgressRecords");
            migrationBuilder.DisableTenantRls("Assignments");

            // Drops each table's indexes, check constraints and foreign keys with it.
            migrationBuilder.DropTable(
                name: "AssignmentProgressRecords");

            migrationBuilder.DropTable(
                name: "Assignments");
        }
    }
}
