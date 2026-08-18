using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.28 - the input sufficiency threshold (roadmap 40.28).
    ///
    /// <para>
    /// <b>The refusal is a state, and the database is what makes it a coherent one.</b> A run that
    /// says "insufficient" without saying what is missing is a dead end on a screen; a run that
    /// carries a list of gaps while it is happily generating is a warning about a lesson nobody
    /// needed. CK_ContentGenerationJobs_Insufficiency is that biconditional stated once, in the place
    /// a second writer cannot forget it: Insufficiency is non-null exactly when Status is
    /// 'insufficient'.
    /// </para>
    ///
    /// <para>
    /// <b>StructuredMaterialLength is what makes a refusal cheap to argue with.</b> The whole point of
    /// refusing rather than generating fifteen bland exercises is that the customer can answer -
    /// "добавьте примеры возражений" only helps if adding them is possible. Adding material appends
    /// to SourceMaterial and resumes the run, and this column records how much of it has already been
    /// read and paid for, so the next structuring call is sent only the tail. Its CHECK bounds it to
    /// the material that actually exists, because the column is a substring index and an index past
    /// the end of the string is a crash in a background worker.
    /// </para>
    ///
    /// <para>
    /// <b>Two constraints are dropped and recreated rather than edited</b> - Postgres has no ALTER
    /// CONSTRAINT for a CHECK expression. CK_..._Status gains the sixth state; CK_..._Counters gains
    /// the new column. Both are recreated with the same names, so a database that has been through
    /// both migrations is indistinguishable from one created fresh.
    /// </para>
    ///
    /// <para>
    /// <b>No index, and that is a decision rather than an omission.</b> The only query that filters on
    /// the new state is the administrator's list, which already runs on
    /// IX_ContentGenerationJobs_OrganizationId_Status_CreatedAt. Nothing queries inside the
    /// Insufficiency document - it is read whole, by primary key or alongside a row already being
    /// listed - so a GIN index on it would be maintenance nobody reads. Hence no
    /// docs/TENANCY/sql/40.28_*_indexes_concurrently.sql: there is no long index to build. The two
    /// ADD COLUMNs are metadata-only in Postgres 11+ (a nullable column and a non-volatile default),
    /// so this migration does not rewrite the table.
    /// </para>
    /// </summary>
    public partial class AddContentGenerationSufficiency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Insufficiency",
                table: "ContentGenerationJobs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StructuredMaterialLength",
                table: "ContentGenerationJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    DROP CONSTRAINT "CK_ContentGenerationJobs_Status";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    ADD CONSTRAINT "CK_ContentGenerationJobs_Status"
                    CHECK ("Status" IN (
                        'structuring', 'awaiting_review', 'generating', 'completed', 'failed', 'insufficient'
                    ));
                """);

            // The refusal and the state are the same fact, so neither may exist without the other. A
            // refused run with no list is a dead end on the review screen; a list left behind on a run
            // that has moved on reads as a warning about the lesson it is about to produce.
            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    ADD CONSTRAINT "CK_ContentGenerationJobs_Insufficiency"
                    CHECK (
                        ("Status" = 'insufficient') = ("Insufficiency" IS NOT NULL)
                    );
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    DROP CONSTRAINT "CK_ContentGenerationJobs_Counters";
                """);

            // StructuredMaterialLength is an offset into SourceMaterial, and the worker slices the
            // material with it. Past the end of the string that slice throws inside a background job,
            // which is the least visible place in the service for an off-by-one to live.
            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    ADD CONSTRAINT "CK_ContentGenerationJobs_Counters"
                    CHECK (
                        "Attempts" >= 0
                        AND "ProducedExerciseCount" >= 0
                        AND "StructuredMaterialLength" >= 0
                        AND "StructuredMaterialLength" <= length("SourceMaterial")
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    DROP CONSTRAINT "CK_ContentGenerationJobs_Counters";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    ADD CONSTRAINT "CK_ContentGenerationJobs_Counters"
                    CHECK ("Attempts" >= 0 AND "ProducedExerciseCount" >= 0);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    DROP CONSTRAINT "CK_ContentGenerationJobs_Insufficiency";
                """);

            // Rows in the state this migration introduced would fail the narrower CHECK, so they are
            // sent back to the checkpoint if they have a structure and to structuring if they do not.
            // Down migrations that leave a table unable to accept its own constraint are down
            // migrations nobody can run.
            migrationBuilder.Sql("""
                UPDATE "ContentGenerationJobs"
                    SET "Status" = CASE WHEN "Structure" IS NULL THEN 'structuring' ELSE 'awaiting_review' END
                    WHERE "Status" = 'insufficient';
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    DROP CONSTRAINT "CK_ContentGenerationJobs_Status";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "ContentGenerationJobs"
                    ADD CONSTRAINT "CK_ContentGenerationJobs_Status"
                    CHECK ("Status" IN ('structuring', 'awaiting_review', 'generating', 'completed', 'failed'));
                """);

            migrationBuilder.DropColumn(
                name: "Insufficiency",
                table: "ContentGenerationJobs");

            migrationBuilder.DropColumn(
                name: "StructuredMaterialLength",
                table: "ContentGenerationJobs");
        }
    }
}
