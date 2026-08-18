using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.10 — stage C of the multi-tenancy roadmap starts here, in learning-db, because that
    /// is where user progress lives.
    ///
    /// <para>
    /// What this migration deliberately does NOT contain: a single <c>CREATE INDEX</c>. The roadmap
    /// puts the <c>(OrganizationId, ...)</c> index rebuilds in a separate operational step, run by
    /// hand with <c>CREATE INDEX CONCURRENTLY</c> in a maintenance window
    /// (docs/TENANCY/sql/40.10_learning_organization_indexes_concurrently.sql), because a
    /// transactional index build on a live progress table takes an <c>ACCESS EXCLUSIVE</c> lock and
    /// this migration runs from <c>Database.Migrate()</c> during startup, where it would stall
    /// readiness and race the replicas. The EF model snapshot still declares those indexes, so this
    /// is the one place in learning-service where the snapshot intentionally runs ahead of what a
    /// migration creates — see docs/DECISIONS.md (2026-08-15, "40.10 index rebuilds are an
    /// operational step").
    /// </para>
    ///
    /// <para>
    /// Nor does it contain the backfill. Existing progress rows get <c>OrganizationId</c> =
    /// all-zeros here and are made real by
    /// docs/TENANCY/sql/40.10_learning_organization_backfill.sql, which knows the default
    /// organization id (learning-db has no tenant registry to read it from). Until that script runs,
    /// the RLS policies below hide every pre-existing progress row — fail closed, per
    /// docs/TENANCY/TENANCY.md 1.5 — so the two steps belong in the same maintenance window. That
    /// ordering requirement is written up in Russian in docs/DONT_FORGET.md.
    /// </para>
    /// </summary>
    public partial class AddOrganizationId : Migration
    {
        private static readonly string[] TenantDataTables =
        [
            "UserSkillProgressRecords",
            "UserLessonProgressRecords",
            "UserExerciseAttempts",
            "UserTechniqueProgress"
        ];

        private static readonly string[] ContentTables =
        [
            "Skills",
            "Topics",
            "Lessons",
            "Exercises",
            "Techniques",
            "ReferenceMaterials"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tenant data: NOT NULL. The zero guid is a placeholder that the backfill script
            // replaces; the DEFAULT is dropped straight afterwards so a future insert that forgets
            // the organization fails loudly instead of quietly landing in the zero tenant.
            foreach (var tableName in TenantDataTables)
            {
                migrationBuilder.AddColumn<Guid>(
                    name: "OrganizationId",
                    table: tableName,
                    type: "uuid",
                    nullable: false,
                    defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

                migrationBuilder.Sql(
                    $"ALTER TABLE \"{tableName}\" ALTER COLUMN \"OrganizationId\" DROP DEFAULT;");
            }

            // Content: nullable, and NULL is not "unknown" — it means "global library, shared by
            // every organization" (docs/TENANCY/CONTENT_MODEL.md). The backfill must leave these
            // alone; filling them in would fork the shared curriculum into one customer's copy.
            foreach (var tableName in ContentTables)
            {
                migrationBuilder.AddColumn<Guid>(
                    name: "OrganizationId",
                    table: tableName,
                    type: "uuid",
                    nullable: true);
            }

            foreach (var tableName in TenantDataTables)
            {
                migrationBuilder.EnableTenantRls(tableName);
            }

            foreach (var tableName in ContentTables)
            {
                migrationBuilder.EnableTenantRlsForContent(tableName);
            }

            // ExerciseTypePrompts, SkillStages and DailyQuotes stay platform-global and outside RLS
            // on purpose (roadmap 40.10). UserReplicas is a projection of identity's user table and
            // TechniqueSkills / TechniqueCoaches are owned rows that cascade from Techniques.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tableName in TenantDataTables)
            {
                migrationBuilder.DisableTenantRls(tableName);
            }

            foreach (var tableName in ContentTables)
            {
                migrationBuilder.DisableTenantRls(tableName);
            }

            foreach (var tableName in TenantDataTables)
            {
                migrationBuilder.DropColumn(name: "OrganizationId", table: tableName);
            }

            foreach (var tableName in ContentTables)
            {
                migrationBuilder.DropColumn(name: "OrganizationId", table: tableName);
            }
        }
    }
}
