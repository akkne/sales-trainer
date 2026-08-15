using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Identity.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationReplicaAndImpersonationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Neither table calls EnableTenantRls, and that is deliberate (Phase 40.9):
            //   * OrganizationReplicas is consulted while deciding whether a token may be issued
            //     at all — before there is a tenant context to filter by, exactly like
            //     OrganizationAuthConfigurations (40.8).
            //   * ImpersonationAuditEntries exists to record crossings *between* tenants, and its
            //     readers are platform staff, not the organization named in the row.
            // See docs/TENANCY/TENANCY.md §1.2 ("platform-global") and docs/DECISIONS.md.
            migrationBuilder.CreateTable(
                name: "ImpersonationAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpersonationAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationReplicas",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationReplicas", x => x.OrganizationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImpersonationAuditEntries_IssuedAt",
                table: "ImpersonationAuditEntries",
                column: "IssuedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ImpersonationAuditEntries_OrganizationId_IssuedAt",
                table: "ImpersonationAuditEntries",
                columns: new[] { "OrganizationId", "IssuedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImpersonationAuditEntries");

            migrationBuilder.DropTable(
                name: "OrganizationReplicas");
        }
    }
}
