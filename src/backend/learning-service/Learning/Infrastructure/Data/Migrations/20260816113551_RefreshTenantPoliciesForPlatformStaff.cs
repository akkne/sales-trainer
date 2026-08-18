using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Learning.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Re-applies every tenant policy in learning-db so its <c>USING</c> clause also admits
    /// validated platform staff (the owner's role split, 2026-08-16 — see docs/DECISIONS.md and
    /// docs/TENANCY/TENANCY.md §1.6).
    ///
    /// <para>
    /// The policy text is not written here: the migration calls the same
    /// <c>EnableTenantRls</c> / <c>EnableTenantRlsForContent</c> helpers the original 40.10
    /// migration called, which now drop and re-create rather than fail on an existing policy. That
    /// is deliberate — a hand-written copy of the policy SQL in a migration is a second definition,
    /// and the two would drift the first time the helper changed again.
    /// </para>
    ///
    /// <para>
    /// <c>Down</c> passes <c>admitPlatformStaff: false</c> through the same helpers, so the
    /// rollback is the exact pre-change policy rather than an approximation of it. The table lists
    /// are copied from 20260815152225_AddOrganizationId and must stay in step with it.
    /// </para>
    ///
    /// <para>
    /// The model is untouched — this migration adds, drops and alters nothing. The snapshot is
    /// therefore identical to the previous one, which is expected, not an oversight.
    /// </para>
    /// </summary>
    public partial class RefreshTenantPoliciesForPlatformStaff : Migration
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
            foreach (var tableName in TenantDataTables)
            {
                migrationBuilder.EnableTenantRls(tableName);
            }

            foreach (var tableName in ContentTables)
            {
                migrationBuilder.EnableTenantRlsForContent(tableName);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tableName in TenantDataTables)
            {
                migrationBuilder.EnableTenantRls(tableName, admitPlatformStaff: false);
            }

            foreach (var tableName in ContentTables)
            {
                migrationBuilder.EnableTenantRlsForContent(tableName, admitPlatformStaff: false);
            }
        }
    }
}
