using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Company.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyPersonas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyPersonas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Position = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Personality = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Medium"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPersonas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyPersonas_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPersonas_CompanyId_CreatedAt",
                table: "CompanyPersonas",
                columns: new[] { "CompanyId", "CreatedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyPersonas");
        }
    }
}
