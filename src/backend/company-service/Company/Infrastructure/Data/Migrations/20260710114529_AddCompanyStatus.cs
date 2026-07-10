using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Company.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Companies",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Lead");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Companies");
        }
    }
}
