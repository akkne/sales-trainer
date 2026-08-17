using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.16 — progress bound to a lesson version (docs/TENANCY/CONTENT_MODEL.md §2.3).
    /// 40.15 gave lessons an immutable history; this points the two progress tables at it, so that
    /// an administrator fixing a wrong correct-answer stops silently re-scoring attempts that were
    /// taken against the old content.
    ///
    /// <para>
    /// <b>Two columns and nothing else, on purpose.</b> Both are nullable, so Postgres 11+ adds them
    /// as catalogue-only changes — no table rewrite, no long lock, on tables that grow with every
    /// answered exercise. The two things a naive version of this migration would also do here are
    /// both deliberately elsewhere:
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><description><b>The indexes</b> are declared in the entity configurations (so the model
    /// snapshot carries them) but created by
    /// <c>docs/TENANCY/sql/40.16_progress_version_indexes_concurrently.sql</c> through
    /// <c>CREATE INDEX CONCURRENTLY</c>. Exactly the judgement 40.10 made about every index on these
    /// same two tables, and for the same reason: an <c>ACCESS EXCLUSIVE</c> lock taken from
    /// <c>Database.Migrate()</c> during service startup is an outage. 40.15 could put its indexes in
    /// the migration because its tables were empty or a few hundred rows; these are not.
    /// </description></item>
    /// <item><description><b>The historical backfill</b> is
    /// <c>docs/TENANCY/sql/40.16_progress_version_backfill.sql</c>, run by hand after the service has
    /// started once. It cannot run here, because it needs a "version 1" to point at and no existing
    /// lesson has one — and that version's snapshot has to be produced by
    /// <c>LessonSnapshotSerializer</c> in C#, not by SQL. Postgres orders <c>jsonb</c> keys by length
    /// and then bytes; the serializer orders them ordinally and hashes the exact bytes it emitted. A
    /// backfill written in SQL would therefore store a hash that does not match what the service
    /// computes, and the next publish would mint a pointless version — defeating the one thing
    /// <c>content_hash</c> exists for. The C# that mints it lives in <c>LessonVersionBackfillService</c>
    /// and runs at startup.
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// <b>No window in which anything is invisible</b>, unlike 40.10-40.13. Nothing filters on these
    /// columns, so a <c>NULL</c> hides no row; until the backfill runs, such attempts simply report
    /// as the metrics endpoint's "unversioned" bucket. The backfill is therefore not a maintenance
    /// pairing and does not have to be run in the same window as the deployment.
    /// </para>
    /// </summary>
    public partial class AddProgressLessonVersionBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LessonVersionId",
                table: "UserLessonProgressRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LessonVersionId",
                table: "UserExerciseAttempts",
                type: "uuid",
                nullable: true);

            // No foreign key to LessonVersions, deliberately. These two tables are strict tenant data
            // under a row-level-security policy of plain equality; LessonVersions is a content table
            // whose policy also admits the global library. A foreign key is validated with the
            // referencing statement's privileges, so the day the service runs as a NOBYPASSRLS role
            // the check would see whatever the current policy lets it see and reject rows that exist.
            // ExerciseId has never carried one either.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping a column drops the indexes over it, including the ones built by the
            // concurrent-index script rather than by Up().
            migrationBuilder.DropColumn(
                name: "LessonVersionId",
                table: "UserLessonProgressRecords");

            migrationBuilder.DropColumn(
                name: "LessonVersionId",
                table: "UserExerciseAttempts");
        }
    }
}
