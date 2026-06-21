using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Social.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FriendshipCanonicalPair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add stored computed columns for the canonical (low, high) pair.
            // LEAST/GREATEST on uuid compares lexicographically, which is fine for uniqueness.
            migrationBuilder.AddColumn<Guid>(
                name: "CanonicalLowId",
                table: "Friendships",
                type: "uuid",
                nullable: false,
                computedColumnSql: "LEAST(\"RequesterId\", \"AddresseeId\")",
                stored: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CanonicalHighId",
                table: "Friendships",
                type: "uuid",
                nullable: false,
                computedColumnSql: "GREATEST(\"RequesterId\", \"AddresseeId\")",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_CanonicalPair",
                table: "Friendships",
                columns: new[] { "CanonicalLowId", "CanonicalHighId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Friendships_CanonicalPair",
                table: "Friendships");

            migrationBuilder.DropColumn(
                name: "CanonicalHighId",
                table: "Friendships");

            migrationBuilder.DropColumn(
                name: "CanonicalLowId",
                table: "Friendships");
        }
    }
}
