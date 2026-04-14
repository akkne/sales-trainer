using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIconicNameToSkillsAndTopics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IconicName",
                table: "Topics",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IconicName",
                table: "Skills",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_IconicName",
                table: "Topics",
                column: "IconicName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_IconicName",
                table: "Skills",
                column: "IconicName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Topics_IconicName",
                table: "Topics");

            migrationBuilder.DropIndex(
                name: "IX_Skills_IconicName",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "IconicName",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "IconicName",
                table: "Skills");
        }
    }
}
