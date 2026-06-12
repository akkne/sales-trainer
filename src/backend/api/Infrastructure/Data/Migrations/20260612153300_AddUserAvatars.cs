using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAvatars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarKey",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AvatarType",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefaultAvatarIndex",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DefaultAvatars",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    ObjectKey = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefaultAvatars", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DefaultAvatars_Index",
                table: "DefaultAvatars",
                column: "Index",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DefaultAvatars");

            migrationBuilder.DropColumn(
                name: "AvatarKey",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AvatarType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DefaultAvatarIndex",
                table: "Users");
        }
    }
}
