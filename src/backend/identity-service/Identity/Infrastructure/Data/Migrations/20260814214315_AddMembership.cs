using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Identity.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddMembership : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Memberships",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Role = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                InvitedBy = table.Column<Guid>(type: "uuid", nullable: true),
                JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeactivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Memberships", x => new { x.UserId, x.OrganizationId });
                table.ForeignKey(
                    name: "FK_Memberships_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Memberships_OrganizationId",
            table: "Memberships",
            column: "OrganizationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Memberships");
    }
}
