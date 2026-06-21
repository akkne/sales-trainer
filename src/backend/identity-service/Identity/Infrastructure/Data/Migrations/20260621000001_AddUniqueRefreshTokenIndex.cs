using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Identity.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddUniqueRefreshTokenIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_RefreshTokens_Token",
            table: "RefreshTokens");

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_Token",
            table: "RefreshTokens",
            column: "Token",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_RefreshTokens_Token",
            table: "RefreshTokens");

        migrationBuilder.CreateIndex(
            name: "IX_RefreshTokens_Token",
            table: "RefreshTokens",
            column: "Token");
    }
}
