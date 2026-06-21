using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Gamification.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddXpRecordSourceEventIdIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserStreaks_UserId",
                table: "UserStreaks");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceEventId",
                table: "UserXpRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserXpRecords_SourceEventId",
                table: "UserXpRecords",
                column: "SourceEventId",
                unique: true,
                filter: "\"SourceEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserStreaks_UserId",
                table: "UserStreaks",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserXpRecords_SourceEventId",
                table: "UserXpRecords");

            migrationBuilder.DropIndex(
                name: "IX_UserStreaks_UserId",
                table: "UserStreaks");

            migrationBuilder.DropColumn(
                name: "SourceEventId",
                table: "UserXpRecords");

            migrationBuilder.CreateIndex(
                name: "IX_UserStreaks_UserId",
                table: "UserStreaks",
                column: "UserId");
        }
    }
}
