using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Ai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDialogBundleIsHidden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "DialogBundles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "DialogBundles");
        }
    }
}
