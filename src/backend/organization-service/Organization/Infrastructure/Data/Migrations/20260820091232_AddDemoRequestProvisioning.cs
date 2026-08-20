using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Organization.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoRequestProvisioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BootstrapAdminEmail",
                table: "DemoRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BootstrapInviteId",
                table: "DemoRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "DemoRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProvisionedAt",
                table: "DemoRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvisioningState",
                table: "DemoRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotProvisioned");

            migrationBuilder.CreateIndex(
                name: "IX_DemoRequests_OrganizationId",
                table: "DemoRequests",
                column: "OrganizationId",
                unique: true,
                filter: "\"OrganizationId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DemoRequests_OrganizationId",
                table: "DemoRequests");

            migrationBuilder.DropColumn(
                name: "BootstrapAdminEmail",
                table: "DemoRequests");

            migrationBuilder.DropColumn(
                name: "BootstrapInviteId",
                table: "DemoRequests");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "DemoRequests");

            migrationBuilder.DropColumn(
                name: "ProvisionedAt",
                table: "DemoRequests");

            migrationBuilder.DropColumn(
                name: "ProvisioningState",
                table: "DemoRequests");
        }
    }
}
