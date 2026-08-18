using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Gamification.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.13 — Stage C reaches gamification-db.
    ///
    /// <para>
    /// Seven tables become tenant data: a person's XP ledger, their streak, their unlocked
    /// achievements, the learning counters that drive those unlocks, and the weekly leagues plus
    /// memberships and period state that make up a competition. Every policy is the <b>strict</b>
    /// flavour (<c>EnableTenantRls</c>, plain equality) — there is no global content library in this
    /// database, so a row with no organization must be invisible, not shared.
    /// </para>
    ///
    /// <para>
    /// Seven other tables deliberately get no column and no policy: <c>Achievements</c> and
    /// <c>LeagueTiers</c> are catalogues, <c>GamificationSettings</c>, <c>StreakMilestones</c> and
    /// <c>ExerciseTypeRewards</c> are installation-wide configuration, <c>UserReplicas</c> projects
    /// identity's cross-organization user directory (TENANCY.md §4.2, same call as 40.10/40.11), and
    /// <c>OutboxMessages</c> is read only by the system-mode relay.
    /// </para>
    ///
    /// <para>
    /// The column lands with an all-zeros placeholder default so the DDL does not rewrite every row
    /// twice. The real organization is written by
    /// <c>docs/TENANCY/sql/40.13_gamification_organization_backfill.sql</c>, an operational step
    /// deliberately kept out of this migration.
    /// </para>
    ///
    /// <para>
    /// <b>Index work is split, and the split is the interesting part.</b> 40.10–40.12 moved every
    /// index into a concurrent-rebuild script, because none of those indexes was load-bearing for
    /// correctness in the window between the deploy and the script. Three here are:
    /// </para>
    /// <list type="bullet">
    /// <item><c>UNIQUE(WeekStartDate, Tier)</c> on <c>Leagues</c> means "one bronze league per week
    /// for the entire platform" — leave it in place and the second organization to roll over gets a
    /// unique violation and no league at all.</item>
    /// <item><c>UNIQUE(UserId)</c> on <c>UserStreaks</c> and <c>UNIQUE(UserId, AchievementId)</c> on
    /// <c>UserAchievements</c> refuse a second row for a person who belongs to two customers, which
    /// memberships (40.6) made possible.</item>
    /// <item><c>UserLearningProgress</c> is keyed by <c>UserId</c> alone, so the same person in two
    /// organizations would have had one organization silently overwrite the other's counters.</item>
    /// </list>
    /// <para>
    /// Those four are swapped here, non-concurrently, and the tables are small enough for that to be
    /// a short lock (at most one row per user, and a handful of rows per week for leagues). The read
    /// indexes on the two tables that actually grow — <c>UserXpRecords</c> and
    /// <c>LeagueMemberships</c> — stay out of this migration and are rebuilt with
    /// <c>CREATE INDEX CONCURRENTLY</c> by
    /// <c>docs/TENANCY/sql/40.13_gamification_organization_indexes_concurrently.sql</c>, old index
    /// dropped only after the replacement is verified valid. The EF model snapshot therefore
    /// describes the post-rollout index set, which this migration does not fully produce; that
    /// divergence is intentional and matches 40.10–40.12.
    /// </para>
    /// </summary>
    public partial class AddOrganizationId : Migration
    {
        private static readonly string[] TenantScopedTables =
        [
            "UserXpRecords",
            "UserStreaks",
            "UserLearningProgress",
            "UserAchievements",
            "LeagueSettings",
            "Leagues",
            "LeagueMemberships"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var tableName in TenantScopedTables)
            {
                migrationBuilder.AddColumn<Guid>(
                    name: "OrganizationId",
                    table: tableName,
                    type: "uuid",
                    nullable: false,
                    defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            }

            // --- Correctness-critical constraint swaps. See the class remarks for why these four
            // --- cannot wait for the concurrent-rebuild script.

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserLearningProgress",
                table: "UserLearningProgress");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserLearningProgress",
                table: "UserLearningProgress",
                columns: new[] { "OrganizationId", "UserId" });

            migrationBuilder.DropIndex(
                name: "IX_UserStreaks_UserId",
                table: "UserStreaks");

            migrationBuilder.CreateIndex(
                name: "IX_UserStreaks_OrganizationId_UserId",
                table: "UserStreaks",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_UserAchievements_UserId_AchievementId",
                table: "UserAchievements");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_OrganizationId_UserId_AchievementId",
                table: "UserAchievements",
                columns: new[] { "OrganizationId", "UserId", "AchievementId" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_Leagues_WeekStartDate_Tier",
                table: "Leagues");

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_OrganizationId_WeekStartDate_Tier",
                table: "Leagues",
                columns: new[] { "OrganizationId", "WeekStartDate", "Tier" },
                unique: true);

            // One league period per organization. New index rather than a swap: LeagueSettings had
            // no unique constraint before, because there was only ever meant to be one row.
            migrationBuilder.CreateIndex(
                name: "IX_LeagueSettings_OrganizationId",
                table: "LeagueSettings",
                column: "OrganizationId",
                unique: true);

            foreach (var tableName in TenantScopedTables)
            {
                migrationBuilder.EnableTenantRls(tableName);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tableName in TenantScopedTables)
            {
                migrationBuilder.DisableTenantRls(tableName);
            }

            migrationBuilder.DropIndex(
                name: "IX_LeagueSettings_OrganizationId",
                table: "LeagueSettings");

            migrationBuilder.DropIndex(
                name: "IX_Leagues_OrganizationId_WeekStartDate_Tier",
                table: "Leagues");

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_WeekStartDate_Tier",
                table: "Leagues",
                columns: new[] { "WeekStartDate", "Tier" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_UserAchievements_OrganizationId_UserId_AchievementId",
                table: "UserAchievements");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_AchievementId",
                table: "UserAchievements",
                columns: new[] { "UserId", "AchievementId" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_UserStreaks_OrganizationId_UserId",
                table: "UserStreaks");

            migrationBuilder.CreateIndex(
                name: "IX_UserStreaks_UserId",
                table: "UserStreaks",
                column: "UserId",
                unique: true);

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserLearningProgress",
                table: "UserLearningProgress");

            foreach (var tableName in TenantScopedTables)
            {
                migrationBuilder.DropColumn(
                    name: "OrganizationId",
                    table: tableName);
            }

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserLearningProgress",
                table: "UserLearningProgress",
                column: "UserId");
        }
    }
}
