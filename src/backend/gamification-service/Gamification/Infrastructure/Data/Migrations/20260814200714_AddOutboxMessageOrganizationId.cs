using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Gamification.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxMessageOrganizationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "OutboxMessages");
        }
    }
}
