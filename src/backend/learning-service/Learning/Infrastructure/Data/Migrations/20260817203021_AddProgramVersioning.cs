using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.17 - programme versioning and enrollment (docs/TENANCY/CONTENT_MODEL.md 2.5). Stage
    /// D of the tenancy roadmap: 40.15 gave a lesson a history, 40.16 bound progress to it, and this
    /// gives an organization's curriculum the same treatment one level up.
    ///
    /// <para>
    /// <b>Three tables, all strict tenant data.</b> Unlike Lessons and LessonVersions there is no
    /// global flavour: a curriculum is a decision one organization made about its own people, so
    /// OrganizationId is NOT NULL and every policy is plain equality rather than
    /// "IS NULL OR = current". A NULL owner here would mean "everybody's programme", which is not a
    /// thing.
    /// </para>
    ///
    /// <para>
    /// <b>Why the indexes are in the migration and there is no 40.17_*_indexes_concurrently.sql.</b>
    /// The same call 40.15 made, for the same reason: all three tables are created empty by this
    /// very migration, so every index is built over zero rows and the ACCESS EXCLUSIVE lock a
    /// transactional build takes costs nothing. 40.10-40.13 needed concurrent scripts because they
    /// were rebuilding indexes on tables that were already large and already live; nothing here has
    /// that shape. Two of the indexes are correctness constraints (one draft per organization, one
    /// pin per learner), and deferring a correctness constraint to a script somebody has to remember
    /// to run is the worse trade.
    /// </para>
    ///
    /// <para>
    /// <b>And no backfill, deliberately.</b> No existing user is enrolled and no "programme version
    /// 1" is minted from the live tree. 40.16 did mint a lesson's version 1 because the lesson body
    /// existed and the snapshot was simply missing; a programme version is not a snapshot of
    /// something that exists but a curriculum decision nobody has made yet, and pinning every
    /// existing learner to whatever the seeder happened to load would freeze them onto it while
    /// telling them nothing. Absent enrollment the service behaves exactly as it did before this
    /// migration - see docs/DECISIONS.md (2026-08-17) and docs/DONT_FORGET.md.
    /// </para>
    /// </summary>
    public partial class AddProgramVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProgramVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "draft"),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProgramEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousProgramVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    EnrolledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SwitchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramEnrollments_ProgramVersions_ProgramVersionId",
                        column: x => x.ProgramVersionId,
                        principalTable: "ProgramVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramItems_ProgramVersions_ProgramVersionId",
                        column: x => x.ProgramVersionId,
                        principalTable: "ProgramVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollments_OrganizationId_UserId",
                table: "ProgramEnrollments",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollments_ProgramVersionId",
                table: "ProgramEnrollments",
                column: "ProgramVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramItems_LessonVersionId",
                table: "ProgramItems",
                column: "LessonVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramItems_OrganizationId_ProgramVersionId_OrderIndex",
                table: "ProgramItems",
                columns: new[] { "OrganizationId", "ProgramVersionId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramItems_ProgramVersionId_LessonId",
                table: "ProgramItems",
                columns: new[] { "ProgramVersionId", "LessonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramVersions_OrganizationId_Draft",
                table: "ProgramVersions",
                column: "OrganizationId",
                unique: true,
                filter: "\"Status\" = 'draft'");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramVersions_OrganizationId_VersionNumber",
                table: "ProgramVersions",
                columns: new[] { "OrganizationId", "VersionNumber" },
                unique: true);

            // Invariants the model cannot state: the status vocabulary, the fact that a published
            // version without a publication time is not a published version, and that a position in
            // a running order is not negative.
            migrationBuilder.Sql("""
                ALTER TABLE "ProgramVersions"
                    ADD CONSTRAINT "CK_ProgramVersions_Status"
                    CHECK ("Status" IN ('draft', 'published', 'archived'));

                ALTER TABLE "ProgramVersions"
                    ADD CONSTRAINT "CK_ProgramVersions_PublishedAt"
                    CHECK ("Status" <> 'published' OR "PublishedAt" IS NOT NULL);

                ALTER TABLE "ProgramItems"
                    ADD CONSTRAINT "CK_ProgramItems_OrderIndex"
                    CHECK ("OrderIndex" >= 0);
                """);

            // Strict tenant isolation on all three: a programme, its items and its enrollments each
            // belong to exactly one organization (docs/TENANCY/TENANCY.md 1.5).
            migrationBuilder.EnableTenantRls("ProgramVersions");
            migrationBuilder.EnableTenantRls("ProgramItems");
            migrationBuilder.EnableTenantRls("ProgramEnrollments");

            // "A published programme is frozen, structure included" is the property this block
            // exists for, so it is enforced where it cannot be bypassed - the same call 40.15 made
            // for lesson versions, and for a sharper reason. A lesson version edited after the fact
            // corrupts a metric; a programme version edited after the fact rearranges the curriculum
            // under somebody who is on lesson 8 of 21, which is the exact sentence in the roadmap.
            //
            // CreatedBy is deliberately left writable: publishing may attribute a draft that was
            // opened by a background path, and authorship is bookkeeping rather than structure.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION "program_version_reject_frozen_change"()
                RETURNS trigger AS $BODY$
                BEGIN
                    IF OLD."Status" = 'draft' THEN
                        RETURN NEW;
                    END IF;

                    IF NEW."VersionNumber" IS DISTINCT FROM OLD."VersionNumber"
                        OR NEW."OrganizationId" IS DISTINCT FROM OLD."OrganizationId"
                        OR NEW."PublishedAt" IS DISTINCT FROM OLD."PublishedAt"
                    THEN
                        RAISE EXCEPTION
                            'ProgramVersion % is %, and a published programme version is frozen forever. '
                            'Editing it would rearrange the programme of every learner pinned to it. '
                            'Open a new draft instead.',
                            OLD."Id", OLD."Status";
                    END IF;

                    IF OLD."Status" = 'published' AND NEW."Status" NOT IN ('published', 'archived') THEN
                        RAISE EXCEPTION
                            'ProgramVersion % cannot go from published back to %.', OLD."Id", NEW."Status";
                    END IF;

                    IF OLD."Status" = 'archived' AND NEW."Status" <> 'archived' THEN
                        RAISE EXCEPTION
                            'ProgramVersion % cannot leave archived.', OLD."Id";
                    END IF;

                    RETURN NEW;
                END;
                $BODY$ LANGUAGE plpgsql;

                CREATE TRIGGER "ProgramVersions_reject_frozen_change"
                    BEFORE UPDATE ON "ProgramVersions"
                    FOR EACH ROW
                    EXECUTE FUNCTION "program_version_reject_frozen_change"();
                """);

            // The item trigger is the one that matters: the structure lives in these rows, so this
            // is where a retroactive reorder would actually be written. It covers DELETE as well as
            // INSERT and UPDATE, because removing a lesson from a frozen programme is the same edit
            // seen from the other side.
            //
            // The "parent row is gone" branch is how a legitimate cascade gets through. Postgres
            // runs ON DELETE CASCADE as an action after the parent row is deleted, so a lookup that
            // finds nothing means the programme version itself is being dropped and these items are
            // going with it. It is not a hole for a write with the wrong tenant either: an INSERT or
            // UPDATE whose organization does not match the session GUC is refused by this table's own
            // RLS WITH CHECK clause before the lookup can be fooled by RLS hiding the parent, and a
            // session with no GUC at all cannot write these tables in the first place.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION "program_item_reject_frozen_change"()
                RETURNS trigger AS $BODY$
                DECLARE
                    owning_program_version_id uuid;
                    owning_status text;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        owning_program_version_id := OLD."ProgramVersionId";
                    ELSE
                        owning_program_version_id := NEW."ProgramVersionId";
                    END IF;

                    SELECT "Status" INTO owning_status
                    FROM "ProgramVersions"
                    WHERE "Id" = owning_program_version_id;

                    IF NOT FOUND OR owning_status = 'draft' THEN
                        IF TG_OP = 'DELETE' THEN
                            RETURN OLD;
                        END IF;

                        RETURN NEW;
                    END IF;

                    RAISE EXCEPTION
                        'ProgramVersion % is %, and its structure is frozen forever. Changing it would '
                        'rearrange the programme of every learner pinned to it. Open a new draft instead.',
                        owning_program_version_id, owning_status;
                END;
                $BODY$ LANGUAGE plpgsql;

                CREATE TRIGGER "ProgramItems_reject_frozen_change"
                    BEFORE INSERT OR UPDATE OR DELETE ON "ProgramItems"
                    FOR EACH ROW
                    EXECUTE FUNCTION "program_item_reject_frozen_change"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "ProgramItems_reject_frozen_change" ON "ProgramItems";
                DROP FUNCTION IF EXISTS "program_item_reject_frozen_change"();

                DROP TRIGGER IF EXISTS "ProgramVersions_reject_frozen_change" ON "ProgramVersions";
                DROP FUNCTION IF EXISTS "program_version_reject_frozen_change"();
                """);

            migrationBuilder.DisableTenantRls("ProgramEnrollments");
            migrationBuilder.DisableTenantRls("ProgramItems");
            migrationBuilder.DisableTenantRls("ProgramVersions");

            // Drops each table's indexes, check constraints and foreign keys with it.
            migrationBuilder.DropTable(
                name: "ProgramEnrollments");

            migrationBuilder.DropTable(
                name: "ProgramItems");

            migrationBuilder.DropTable(
                name: "ProgramVersions");
        }
    }
}
