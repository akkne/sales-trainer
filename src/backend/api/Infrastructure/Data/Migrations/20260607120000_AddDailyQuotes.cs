using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalesTrainer.Api.Infrastructure.Data;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260607120000_AddDailyQuotes")]
    public partial class AddDailyQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyQuotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Author = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyQuotes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyQuotes_Date",
                table: "DailyQuotes",
                column: "Date",
                unique: true);

            // Seed today's quote with the previously hardcoded widget tip.
            migrationBuilder.Sql(@"
                INSERT INTO ""DailyQuotes"" (""Id"", ""Date"", ""Text"", ""Author"", ""CreatedAt"", ""UpdatedAt"")
                VALUES (
                    gen_random_uuid(),
                    CURRENT_DATE,
                    'Когда клиент говорит «дорого», не называйте скидку. Спросите — «дорого по сравнению с чем?»',
                    'Skeptic Sergey',
                    NOW(),
                    NOW());
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyQuotes");
        }
    }
}
