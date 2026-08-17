using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.18 - copy-on-write overrides (docs/TENANCY/CONTENT_MODEL.md 2.6). 40.15 already
    /// gave Lessons everything an override needs (ParentLessonId, LessonVersion.BaseVersionId), so
    /// this migration only brings the other two content families named by the roadmap - Techniques
    /// and ReferenceMaterials - up to the same shape, and states one invariant the earlier
    /// migrations left unstated.
    ///
    /// <para>
    /// <b>Three columns each, and why they are not a second version table.</b> ParentTechniqueId /
    /// ParentMaterialId are the copy-on-write pointer. BaseContentHash is the fork point: lessons
    /// record it as the id of a frozen LessonVersion, but these two families have no immutable
    /// version table and building two more was out of this block's scope, so the fork point is a
    /// fingerprint of the base row instead. Both answer the only question the staleness queue asks -
    /// "has upstream moved since we forked?" - and the fingerprint gives up only the ability to show
    /// the upstream diff as before/after (docs/DECISIONS.md, 2026-08-18). IsArchived exists because
    /// the review action "take the new base" must retire an override rather than delete it: user
    /// progress points at these rows without a foreign key, and deleting them to make a review
    /// action tidy would orphan that history.
    /// </para>
    ///
    /// <para>
    /// <b>The CHECK constraints matter more than the columns.</b> An override with no owning
    /// organization is a global row that shadows a global row, which makes read resolution hide the
    /// library behind itself - permanently, silently, and for every customer at once. The constraint
    /// is added to Lessons too, where 40.15 created the column without one.
    /// </para>
    ///
    /// <para>
    /// <b>No 40.18_*_indexes_concurrently.sql, deliberately.</b> Every operation here is cheap on
    /// Postgres 11+: a nullable column is a catalog change, a NOT NULL column with a constant
    /// default is a catalog change (attmissingval), and the two new indexes are built over tables
    /// holding dozens of rows, not the millions the 40.10-40.13 progress tables hold. The CHECK
    /// constraints do take a full scan of Techniques, ReferenceMaterials and Lessons - the same
    /// dozens-to-hundreds of rows. There is nothing here worth an operational step somebody has to
    /// remember to run; see docs/DONT_FORGET.md.
    /// </para>
    /// </summary>
    public partial class AddContentOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseContentHash",
                table: "Techniques",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Techniques",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentTechniqueId",
                table: "Techniques",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseContentHash",
                table: "ReferenceMaterials",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "ReferenceMaterials",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentMaterialId",
                table: "ReferenceMaterials",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Techniques_ParentTechniqueId",
                table: "Techniques",
                column: "ParentTechniqueId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceMaterials_ParentMaterialId",
                table: "ReferenceMaterials",
                column: "ParentMaterialId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReferenceMaterials_ReferenceMaterials_ParentMaterialId",
                table: "ReferenceMaterials",
                column: "ParentMaterialId",
                principalTable: "ReferenceMaterials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Techniques_Techniques_ParentTechniqueId",
                table: "Techniques",
                column: "ParentTechniqueId",
                principalTable: "Techniques",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // An override belongs to somebody. A row with a parent and no organization is a global
            // row shadowing a global row, and the resolution rule of CONTENT_MODEL.md 1 ("an
            // override exists -> use it") would then hide the base library behind a copy of itself
            // for every customer at once. Stated in the database rather than in C# because the
            // seeder, the bundle importer and three admin controllers all write these tables.
            migrationBuilder.Sql("""
                ALTER TABLE "Techniques"
                    ADD CONSTRAINT "CK_Techniques_OverrideHasOwner"
                    CHECK ("ParentTechniqueId" IS NULL OR "OrganizationId" IS NOT NULL);

                ALTER TABLE "ReferenceMaterials"
                    ADD CONSTRAINT "CK_ReferenceMaterials_OverrideHasOwner"
                    CHECK ("ParentMaterialId" IS NULL OR "OrganizationId" IS NOT NULL);

                ALTER TABLE "Lessons"
                    ADD CONSTRAINT "CK_Lessons_OverrideHasOwner"
                    CHECK ("ParentLessonId" IS NULL OR "OrganizationId" IS NOT NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Lessons" DROP CONSTRAINT IF EXISTS "CK_Lessons_OverrideHasOwner";
                ALTER TABLE "ReferenceMaterials" DROP CONSTRAINT IF EXISTS "CK_ReferenceMaterials_OverrideHasOwner";
                ALTER TABLE "Techniques" DROP CONSTRAINT IF EXISTS "CK_Techniques_OverrideHasOwner";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ReferenceMaterials_ReferenceMaterials_ParentMaterialId",
                table: "ReferenceMaterials");

            migrationBuilder.DropForeignKey(
                name: "FK_Techniques_Techniques_ParentTechniqueId",
                table: "Techniques");

            migrationBuilder.DropIndex(
                name: "IX_Techniques_ParentTechniqueId",
                table: "Techniques");

            migrationBuilder.DropIndex(
                name: "IX_ReferenceMaterials_ParentMaterialId",
                table: "ReferenceMaterials");

            migrationBuilder.DropColumn(
                name: "BaseContentHash",
                table: "Techniques");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Techniques");

            migrationBuilder.DropColumn(
                name: "ParentTechniqueId",
                table: "Techniques");

            migrationBuilder.DropColumn(
                name: "BaseContentHash",
                table: "ReferenceMaterials");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "ReferenceMaterials");

            migrationBuilder.DropColumn(
                name: "ParentMaterialId",
                table: "ReferenceMaterials");
        }
    }
}
