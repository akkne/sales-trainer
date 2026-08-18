using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Sellevate.BuildingBlocks.Tenancy;

#nullable disable

namespace Sellevate.Social.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 40.13 — Stage C reaches social-db, the last Postgres database in the rollout.
    ///
    /// <para>
    /// Six of the seven tables here are strictly tenant data: a friendship, a thread, a reply, a
    /// vote, a thread-tag link and a photo are all one person saying something to their colleagues,
    /// so every one gets a non-null <c>OrganizationId</c> and the strict RLS policy. <c>DiscussTags</c>
    /// is the single content table in this database — <c>NULL</c> means the curated vocabulary every
    /// organization shares, a value means a tag one customer created for itself — so its column is
    /// nullable and its policy is the content flavour, or a new customer would open Discuss and find
    /// no tags at all. <c>UserReplicas</c> gets nothing, the same call learning (40.10), ai (40.11)
    /// and gamification (40.13) made: it projects identity's cross-organization user directory, and
    /// giving it an organization would mean a Kafka consumer deciding a tenant per message.
    /// </para>
    ///
    /// <para>
    /// The strict columns land with an all-zeros placeholder default so the DDL does not rewrite
    /// every row twice. The real organization is written by
    /// <c>docs/TENANCY/sql/40.13_social_organization_backfill.sql</c>, an operational step
    /// deliberately kept out of this migration. Until it runs, every pre-existing row is hidden by
    /// its own policy — unpleasant, and fail-closed, which is the direction to fail in.
    /// </para>
    ///
    /// <para>
    /// <b>Index work is split, and the split is the point.</b> The read indexes — every
    /// <c>(OrganizationId, …)</c> rebuild on the tables that actually grow — are NOT here. They are
    /// in <c>docs/TENANCY/sql/40.13_social_organization_indexes_concurrently.sql</c>, built with
    /// <c>CREATE INDEX CONCURRENTLY</c> and dropped only after the replacement is verified valid,
    /// because this migration runs from <c>Database.Migrate()</c> during startup and a transactional
    /// index build takes an <c>ACCESS EXCLUSIVE</c> lock (docs/TENANCY/TENANCY.md §3.2). The EF model
    /// snapshot therefore describes the post-rollout index set that this migration does not produce;
    /// that divergence is intentional and matches 40.10–40.12.
    /// </para>
    ///
    /// <para>
    /// Two unique swaps <em>are</em> here, for the same reason gamification's four were: without
    /// them the second organization is broken from the moment this deploys, and neither table is one
    /// that grows.
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>UNIQUE(Slug)</c> on <c>DiscussTags</c> means "one tag named <c>скрипты</c> for the whole
    ///     platform". The first customer to use a word takes it from everybody; the next
    ///     organization's thread creation fails on a constraint violation while they are typing it.
    ///     It becomes <c>UNIQUE(OrganizationId, Slug)</c> plus a partial <c>UNIQUE(Slug) WHERE
    ///     OrganizationId IS NULL</c> — two indexes, not one, because Postgres treats NULLs in a
    ///     composite unique index as distinct and the composite alone would accept the curated tag
    ///     <c>скрипты</c> twice at the global level.
    ///   </description></item>
    ///   <item><description>
    ///     <c>UNIQUE(RequesterId, AddresseeId)</c> and the canonical-pair
    ///     <c>UNIQUE(CanonicalLowId, CanonicalHighId)</c> on <c>Friendships</c> refuse a second
    ///     friendship between two people who happen to be colleagues at two customers — which
    ///     memberships (40.6) made possible — and would have made one organization's friend request
    ///     silently a duplicate of another's.
    ///   </description></item>
    /// </list>
    /// </summary>
    public partial class AddOrganizationId : Migration
    {
        /// <summary>
        /// Strict tenant data: <c>OrganizationId</c> is <c>NOT NULL</c> and a row with no
        /// organization is invisible, never shared.
        /// </summary>
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
                migrationBuilder.AddColumn<Guid>(
                    name: "OrganizationId",
                    table: tableName,
                    type: "uuid",
                    nullable: false,
                    defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            }

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: ContentTable,
                type: "uuid",
                nullable: true);

            // ── the two unique swaps that cannot wait for the operational script ──────────────

            migrationBuilder.DropIndex(
                name: "IX_DiscussTags_Slug",
                table: "DiscussTags");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussTags_OrganizationId_Slug",
                table: "DiscussTags",
                columns: ["OrganizationId", "Slug"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscussTags_Slug_Global",
                table: "DiscussTags",
                column: "Slug",
                unique: true,
                filter: "\"OrganizationId\" IS NULL");

            migrationBuilder.DropIndex(
                name: "IX_Friendships_RequesterId_AddresseeId",
                table: "Friendships");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_OrganizationId_RequesterId_AddresseeId",
                table: "Friendships",
                columns: ["OrganizationId", "RequesterId", "AddresseeId"],
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_Friendships_CanonicalPair",
                table: "Friendships");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_CanonicalPair",
                table: "Friendships",
                columns: ["OrganizationId", "CanonicalLowId", "CanonicalHighId"],
                unique: true);

            // ── row-level security, the layer that survives a forgotten query filter ──────────

            foreach (var tableName in TenantScopedTables)
            {
                migrationBuilder.EnableTenantRls(tableName);
            }

            migrationBuilder.EnableTenantRlsForContent(ContentTable);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls(ContentTable);

            foreach (var tableName in TenantScopedTables)
            {
                migrationBuilder.DisableTenantRls(tableName);
            }

            migrationBuilder.DropIndex(
                name: "IX_Friendships_CanonicalPair",
                table: "Friendships");

            migrationBuilder.DropIndex(
                name: "IX_Friendships_OrganizationId_RequesterId_AddresseeId",
                table: "Friendships");

            migrationBuilder.DropIndex(
                name: "IX_DiscussTags_Slug_Global",
                table: "DiscussTags");

            migrationBuilder.DropIndex(
                name: "IX_DiscussTags_OrganizationId_Slug",
                table: "DiscussTags");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: ContentTable);

            foreach (var tableName in TenantScopedTables)
            {
                migrationBuilder.DropColumn(
                    name: "OrganizationId",
                    table: tableName);
            }

            migrationBuilder.CreateIndex(
                name: "IX_DiscussTags_Slug",
                table: "DiscussTags",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_RequesterId_AddresseeId",
                table: "Friendships",
                columns: ["RequesterId", "AddresseeId"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_CanonicalPair",
                table: "Friendships",
                columns: ["CanonicalLowId", "CanonicalHighId"],
                unique: true);
        }
    }
}
