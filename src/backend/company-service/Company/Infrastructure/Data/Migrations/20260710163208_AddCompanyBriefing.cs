using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Company.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyBriefing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BriefingContent",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BriefingGeneratedAt",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BriefingContent",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "BriefingGeneratedAt",
                table: "Companies");
        }
    }
}
