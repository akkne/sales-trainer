using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Social.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Re-applies every tenant policy in social-db so its <c>USING</c> clause also admits validated
    /// platform staff (the owner's role split, 2026-08-16 — docs/DECISIONS.md,
    /// docs/TENANCY/TENANCY.md §1.6). Same helpers as 20260816081204_AddOrganizationId, which now
    /// replace an existing policy instead of failing on it. <c>Down</c> regenerates the exact
    /// pre-change policies through those same helpers.
    ///
    /// <para>
    /// Mongo's <c>chat_conversations</c> is not affected here and cannot be: it has no row-level
    /// security, so its equivalent widening lives in
    /// <c>ChatConversationRepository.TenantFilter</c>.
    /// </para>
    ///
    /// <para>The model is untouched; an identical snapshot is expected.</para>
    /// </summary>
    public partial class RefreshTenantPoliciesForPlatformStaff : Migration
    {
        private static readonly string[] TenantScopedTables =
        [
            "Friendships",
            "DiscussThreads",
            "DiscussReplies",
            "DiscussVotes",
            "DiscussThreadTags",
            "DiscussPhotos"
        ];

        private const string ContentTable = "DiscussTags";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var tableName in TenantScopedTables)
            {
                migrationBuilder.EnableTenantRls(tableName);
            }

            migrationBuilder.EnableTenantRlsForContent(ContentTable);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tableName in TenantScopedTables)
            {
                migrationBuilder.EnableTenantRls(tableName, admitPlatformStaff: false);
            }

            migrationBuilder.EnableTenantRlsForContent(ContentTable, admitPlatformStaff: false);
        }
    }
}
