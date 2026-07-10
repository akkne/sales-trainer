using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Company.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FollowUpNotifiedAt",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextActionAt",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextActionNote",
                table: "Companies",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_NextActionAt",
                table: "Companies",
                column: "NextActionAt",
                filter: "\"NextActionAt\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Companies_NextActionAt",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "FollowUpNotifiedAt",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "NextActionAt",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "NextActionNote",
                table: "Companies");
        }
    }
}
