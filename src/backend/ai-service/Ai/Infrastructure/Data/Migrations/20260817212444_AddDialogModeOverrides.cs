using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Ai.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.18 - per-organization prompt overrides (docs/TENANCY/CONTENT_MODEL.md 2.6, 4).
    /// 40.11 already made DialogModes a content table: OrganizationId is nullable, null is the
    /// global library, and the key is unique per (OrganizationId, BundleId) with a partial unique
    /// index over the global rows. This adds the two columns copy-on-write needs on top of that.
    ///
    /// <para>
    /// <b>An override keeps its parent's BundleId and Key.</b> That is already legal under the
    /// 40.11 indexes, and it is what makes an overridden prompt show up in the same bundle in the
    /// same position without a second layer of resolution deciding which folder a copy belongs to.
    /// DialogBundles are deliberately left alone: a bundle carries no prompt at all - only a title,
    /// a description, an emoji and a sort order - while every sentence of the roadmap about this
    /// service is about prompts. Copying a bundle would fork its whole mode list, which is the
    /// library fork of CONTENT_MODEL 1 one level down (docs/DECISIONS.md, 2026-08-18).
    /// </para>
    ///
    /// <para>
    /// <b>Nothing long-running.</b> Two nullable columns are a catalog change on Postgres 11+, the
    /// index is built over a table holding tens of rows, and the CHECK scans the same tens. There is
    /// no 40.18 concurrent-index script for this service and does not need to be.
    /// </para>
    /// </summary>
    public partial class AddDialogModeOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseContentHash",
                table: "DialogModes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentModeId",
                table: "DialogModes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DialogModes_ParentModeId",
                table: "DialogModes",
                column: "ParentModeId");

            migrationBuilder.AddForeignKey(
                name: "FK_DialogModes_DialogModes_ParentModeId",
                table: "DialogModes",
                column: "ParentModeId",
                principalTable: "DialogModes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // An override belongs to somebody. A row with a parent and no organization is a global
            // row shadowing a global row, and read resolution would then hide the shared prompt
            // library behind a copy of itself for every customer at once. The two seeded hidden
            // modes stay global because nothing is allowed to override them at all - the service
            // refuses - but the constraint is what makes that a fact rather than a convention.
            migrationBuilder.Sql("""
                ALTER TABLE "DialogModes"
                    ADD CONSTRAINT "CK_DialogModes_OverrideHasOwner"
                    CHECK ("ParentModeId" IS NULL OR "OrganizationId" IS NOT NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """ALTER TABLE "DialogModes" DROP CONSTRAINT IF EXISTS "CK_DialogModes_OverrideHasOwner";""");

            migrationBuilder.DropForeignKey(
                name: "FK_DialogModes_DialogModes_ParentModeId",
                table: "DialogModes");

            migrationBuilder.DropIndex(
                name: "IX_DialogModes_ParentModeId",
                table: "DialogModes");

            migrationBuilder.DropColumn(
                name: "BaseContentHash",
                table: "DialogModes");

            migrationBuilder.DropColumn(
                name: "ParentModeId",
                table: "DialogModes");
        }
    }
}
