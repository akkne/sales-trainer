using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Company.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyReadinessNoFeedbackCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReadinessNoFeedbackUntil",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadinessNoFeedbackUntil",
                table: "Companies");
        }
    }
}
