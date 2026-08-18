using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.24. Two nullable columns that turn a repeat from a copy into a member of a series,
    /// the unique index that is the repeat sweep's entire idempotency story, and three check
    /// constraints that keep a repeat from repeating.
    ///
    /// <para>
    /// <b>No backfill, no maintenance window, no concurrent-index script</b> — the same call 40.21,
    /// 40.22 and 40.23 all made, and for the same reason: <c>Assignments</c> is empty in every
    /// deployed database (nothing could create one before 40.21, and the РОП's admin panel is still
    /// 40.20), so both columns and the index land over zero rows and no existing row changes meaning.
    /// There is deliberately no <c>docs/TENANCY/sql/40.24_*_indexes_concurrently.sql</c>: the index
    /// here is a correctness constraint, not a performance one, and deferring a correctness
    /// constraint to a script somebody has to remember to run is how a "unique" column ends up not
    /// being unique.
    /// </para>
    ///
    /// <para>
    /// <b>The index deliberately does not lead with <c>OrganizationId</c></b>, the second such
    /// exception in this feature. Two reasons, both the ones 40.21 recorded for
    /// <c>IX_AssignmentProgressRecords_AssignmentId_Status</c>: it is the only index covering the new
    /// self-referencing foreign key, so without it Postgres scans the whole table on every attempt to
    /// delete an assignment; and an origin id is globally unique already, so putting the organization
    /// in front would weaken the uniqueness rather than scope it. Isolation is unaffected — the
    /// row-level-security policy, not an index, is what decides who can see a row.
    /// </para>
    ///
    /// <para>
    /// <b>The freeze trigger gains two frozen columns.</b> Which series an assignment belongs to and
    /// which wave it is are identity, and every score recorded against it is read through them; a
    /// row that could be re-pointed at another origin after the fact would make the series comparison
    /// 40.25 is built on describe something that never happened.
    /// </para>
    /// </summary>
    public partial class AddAssignmentRepeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RepeatOfAssignmentId",
                table: "Assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepeatWaveIndex",
                table: "Assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_RepeatOfAssignmentId_RepeatWaveIndex",
                table: "Assignments",
                columns: new[] { "RepeatOfAssignmentId", "RepeatWaveIndex" },
                unique: true,
                filter: "\"RepeatOfAssignmentId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_Assignments_RepeatOfAssignmentId",
                table: "Assignments",
                column: "RepeatOfAssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // The three facts that make a repeat readable. The middle one is the load-bearing one:
            // a repeat that carried a schedule of its own would repeat itself, and two waves would
            // each spawn two more — an exponential fan-out of progress rows and notifications that
            // nobody would catch early, because each individual step looks exactly like the feature
            // working.
            migrationBuilder.Sql("""
                ALTER TABLE "Assignments"
                    ADD CONSTRAINT "CK_Assignments_RepeatWave"
                    CHECK (("RepeatOfAssignmentId" IS NULL) = ("RepeatWaveIndex" IS NULL)
                           AND ("RepeatWaveIndex" IS NULL OR "RepeatWaveIndex" >= 1));

                ALTER TABLE "Assignments"
                    ADD CONSTRAINT "CK_Assignments_RepeatNoCascade"
                    CHECK ("RepeatOfAssignmentId" IS NULL OR "RepeatSchedule" IS NULL);

                ALTER TABLE "Assignments"
                    ADD CONSTRAINT "CK_Assignments_RepeatNotSelf"
                    CHECK ("RepeatOfAssignmentId" IS DISTINCT FROM "Id");
                """);

            // Same function as 40.21's, with RepeatOfAssignmentId and RepeatWaveIndex added to the
            // frozen set. Replaced rather than amended because a plpgsql body has no seams.
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
                        OR NEW."RepeatOfAssignmentId" IS DISTINCT FROM OLD."RepeatOfAssignmentId"
                        OR NEW."RepeatWaveIndex" IS DISTINCT FROM OLD."RepeatWaveIndex"
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to 40.21's body verbatim: the two columns are about to stop existing, and a
            // trigger that names a dropped column fails on the next update of any assignment.
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
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Assignments" DROP CONSTRAINT IF EXISTS "CK_Assignments_RepeatNotSelf";
                ALTER TABLE "Assignments" DROP CONSTRAINT IF EXISTS "CK_Assignments_RepeatNoCascade";
                ALTER TABLE "Assignments" DROP CONSTRAINT IF EXISTS "CK_Assignments_RepeatWave";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_Assignments_RepeatOfAssignmentId",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_RepeatOfAssignmentId_RepeatWaveIndex",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "RepeatOfAssignmentId",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "RepeatWaveIndex",
                table: "Assignments");
        }
    }
}
