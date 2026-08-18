using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.15 — immutable lesson versioning (docs/TENANCY/CONTENT_MODEL.md §2). Stage D of the
    /// tenancy roadmap: 40.10 gave learning-db an owner per row, this gives its lessons a history.
    ///
    /// <para>
    /// <b>Why this migration creates indexes when 40.10 deliberately created none.</b> That
    /// migration's indexes were rebuilds of live progress tables — millions of rows, an
    /// <c>ACCESS EXCLUSIVE</c> lock, taken from <c>Database.Migrate()</c> during startup — so they
    /// went to a hand-run <c>CREATE INDEX CONCURRENTLY</c> script instead. Nothing here is that
    /// shape: <c>LessonVersions</c> is created empty by this very migration, so its indexes cost
    /// nothing, and <c>Lessons</c> is a content table of a few hundred rows where the build is
    /// milliseconds. The same judgement 40.13 made for the four small gamification tables applies,
    /// and for the same reason: the slug uniqueness is a correctness constraint, and leaving a
    /// window where it is not enforced is worse than a lock nobody can measure. There is
    /// consequently no <c>40.15_*_indexes_concurrently.sql</c>, and its absence is deliberate
    /// rather than forgotten — docs/DONT_FORGET.md says so in Russian.
    /// </para>
    ///
    /// <para>
    /// <b>The slug backfill runs here, not in a separate script.</b> Unlike 40.9-40.13, this
    /// backfill needs nothing the database does not already have — the value is derived from each
    /// row's own primary key — so there is no window in which lessons are invisible and no
    /// maintenance pairing for a human to get wrong. The generated form matches
    /// <c>LessonSlugGenerator.GenerateFromLessonId</c> exactly; if one changes, so must the other.
    /// </para>
    /// </summary>
    public partial class AddLessonVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Lessons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentLessonId",
                table: "Lessons",
                type: "uuid",
                nullable: true);

            // Deliberately NOT the generated "nullable: false, defaultValue: \"\"". That would give
            // every existing lesson the same empty slug and the unique index below would then fail
            // on the second row — on startup, in Database.Migrate(), where the failure reads as
            // "the service will not boot". Nullable, backfilled, then tightened.
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Lessons",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Lessons"
                SET "Slug" = 'lesson-' || replace("Id"::text, '-', '')
                WHERE "Slug" IS NULL;

                ALTER TABLE "Lessons" ALTER COLUMN "Slug" SET NOT NULL;
                """);

            migrationBuilder.CreateTable(
                name: "LessonVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "draft"),
                    BaseVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsBreaking = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonVersions_LessonVersions_BaseVersionId",
                        column: x => x.BaseVersionId,
                        principalTable: "LessonVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LessonVersions_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_OrganizationId_Slug",
                table: "Lessons",
                columns: new[] { "OrganizationId", "Slug" },
                unique: true);

            // The 40.10 trap, restated because it is invisible in the line above: Postgres treats
            // NULLs in a composite unique index as distinct, so the index above does NOT stop two
            // global lessons sharing a slug. This partial one over the global rows is what does.
            migrationBuilder.CreateIndex(
                name: "IX_Lessons_Slug_Global",
                table: "Lessons",
                column: "Slug",
                unique: true,
                filter: "\"OrganizationId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_ParentLessonId",
                table: "Lessons",
                column: "ParentLessonId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonVersions_BaseVersionId",
                table: "LessonVersions",
                column: "BaseVersionId");

            // At most one mutable draft per lesson, in the database rather than in C#. Two admins
            // pressing "edit" at the same moment is exactly the race a check-then-insert loses, and
            // the result would be two branches of a lesson with no merge story — merging prose and
            // grading criteria automatically produces plausible nonsense that then grades a
            // salesperson (CONTENT_MODEL.md §2.6).
            migrationBuilder.CreateIndex(
                name: "IX_LessonVersions_LessonId_Draft",
                table: "LessonVersions",
                column: "LessonId",
                unique: true,
                filter: "\"Status\" = 'draft'");

            migrationBuilder.CreateIndex(
                name: "IX_LessonVersions_LessonId_VersionNumber",
                table: "LessonVersions",
                columns: new[] { "LessonId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonVersions_OrganizationId_LessonId_VersionNumber",
                table: "LessonVersions",
                columns: new[] { "OrganizationId", "LessonId", "VersionNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_Lessons_Lessons_ParentLessonId",
                table: "Lessons",
                column: "ParentLessonId",
                principalTable: "Lessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Two invariants the model cannot state: the status vocabulary, and the fact that a
            // published version without a publication time is not a published version.
            migrationBuilder.Sql("""
                ALTER TABLE "LessonVersions"
                    ADD CONSTRAINT "CK_LessonVersions_Status"
                    CHECK ("Status" IN ('draft', 'published', 'archived'));

                ALTER TABLE "LessonVersions"
                    ADD CONSTRAINT "CK_LessonVersions_PublishedAt"
                    CHECK ("Status" <> 'published' OR "PublishedAt" IS NOT NULL);
                """);

            // Content is a tenancy-content table: NULL OrganizationId means the global library
            // every organization reads, so the policy is "mine or global", never plain equality
            // (docs/TENANCY/TENANCY.md §1.5). Same treatment Lessons and Exercises got in 40.10.
            migrationBuilder.EnableTenantRlsForContent("LessonVersions");

            // "Publishing freezes the row forever" is the entire point of the table, so it is
            // enforced by the database and not by the service that writes it. A snapshot that can
            // be edited after the fact is not a snapshot: every historical attempt scored against
            // it would silently re-interpret, which is the exact metric corruption 40.16 exists to
            // fix, arrived at from inside the fix.
            //
            // Three columns are deliberately left writable on a frozen row. BaseVersionId, because
            // 40.18's stale-override review offers "keep the override, re-point its base" as one of
            // its three actions. Status, because retiring a version (published -> archived) is a
            // lifecycle move, not a rewrite -- and the transition check below is what stops that
            // door being used to walk a version back to draft and edit it. CreatedBy/CreatedAt are
            // not writable and not interesting; they are simply never touched.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION "lesson_version_reject_frozen_change"()
                RETURNS trigger AS $BODY$
                BEGIN
                    IF OLD."Status" = 'draft' THEN
                        RETURN NEW;
                    END IF;

                    IF NEW."Content" IS DISTINCT FROM OLD."Content"
                        OR NEW."ContentHash" IS DISTINCT FROM OLD."ContentHash"
                        OR NEW."VersionNumber" IS DISTINCT FROM OLD."VersionNumber"
                        OR NEW."LessonId" IS DISTINCT FROM OLD."LessonId"
                        OR NEW."OrganizationId" IS DISTINCT FROM OLD."OrganizationId"
                        OR NEW."IsBreaking" IS DISTINCT FROM OLD."IsBreaking"
                        OR NEW."PublishedAt" IS DISTINCT FROM OLD."PublishedAt"
                    THEN
                        RAISE EXCEPTION
                            'LessonVersion % is %, and a published lesson version is frozen forever. '
                            'Editing it would silently re-interpret every historical attempt scored '
                            'against it. Open a new draft instead.',
                            OLD."Id", OLD."Status";
                    END IF;

                    IF OLD."Status" = 'published' AND NEW."Status" NOT IN ('published', 'archived') THEN
                        RAISE EXCEPTION
                            'LessonVersion % cannot go from published back to %.', OLD."Id", NEW."Status";
                    END IF;

                    IF OLD."Status" = 'archived' AND NEW."Status" <> 'archived' THEN
                        RAISE EXCEPTION
                            'LessonVersion % cannot leave archived.', OLD."Id";
                    END IF;

                    RETURN NEW;
                END;
                $BODY$ LANGUAGE plpgsql;

                CREATE TRIGGER "LessonVersions_reject_frozen_change"
                    BEFORE UPDATE ON "LessonVersions"
                    FOR EACH ROW
                    EXECUTE FUNCTION "lesson_version_reject_frozen_change"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "LessonVersions_reject_frozen_change" ON "LessonVersions";
                DROP FUNCTION IF EXISTS "lesson_version_reject_frozen_change"();
                """);

            migrationBuilder.DisableTenantRls("LessonVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_Lessons_Lessons_ParentLessonId",
                table: "Lessons");

            // Drops the table's indexes, constraints and both foreign keys with it.
            migrationBuilder.DropTable(
                name: "LessonVersions");

            migrationBuilder.DropIndex(
                name: "IX_Lessons_OrganizationId_Slug",
                table: "Lessons");

            migrationBuilder.DropIndex(
                name: "IX_Lessons_ParentLessonId",
                table: "Lessons");

            migrationBuilder.DropIndex(
                name: "IX_Lessons_Slug_Global",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "ParentLessonId",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Lessons");
        }
    }
}
