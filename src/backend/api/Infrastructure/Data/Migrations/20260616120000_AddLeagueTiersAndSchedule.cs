using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalesTrainer.Api.Infrastructure.Data;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260616120000_AddLeagueTiersAndSchedule")]
    public partial class AddLeagueTiersAndSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeagueTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueTiers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueTiers_Key",
                table: "LeagueTiers",
                column: "Key",
                unique: true);

            // Seed the previously hardcoded tier ladder (key, label, color, order).
            migrationBuilder.Sql(@"
                INSERT INTO ""LeagueTiers"" (""Id"", ""Key"", ""Name"", ""Color"", ""Order"") VALUES
                    (gen_random_uuid(), 'bronze',  'Бронза',  '#c47b3f', 1),
                    (gen_random_uuid(), 'silver',  'Серебро', '#9aa3ad', 2),
                    (gen_random_uuid(), 'gold',    'Золото',  '#e3b23c', 3),
                    (gen_random_uuid(), 'diamond', 'Алмаз',   '#4cc6e8', 4);
            ");

            // Period scheduling fields on the singleton settings row.
            migrationBuilder.AddColumn<DateOnly>(
                name: "CurrentPeriodStartDate",
                table: "LeagueSettings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CurrentPeriodEndsAt",
                table: "LeagueSettings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeriodLengthDays",
                table: "LeagueSettings",
                type: "integer",
                nullable: false,
                defaultValue: 7);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CurrentPeriodStartDate", table: "LeagueSettings");
            migrationBuilder.DropColumn(name: "CurrentPeriodEndsAt", table: "LeagueSettings");
            migrationBuilder.DropColumn(name: "PeriodLengthDays", table: "LeagueSettings");
            migrationBuilder.DropTable(name: "LeagueTiers");
        }
    }
}
