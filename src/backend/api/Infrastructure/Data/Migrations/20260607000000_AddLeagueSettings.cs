using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalesTrainer.Api.Infrastructure.Data;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260607000000_AddLeagueSettings")]
    public partial class AddLeagueSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeagueSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaximumLeagueParticipantCount = table.Column<int>(type: "integer", nullable: false),
                    PromotionZoneSize = table.Column<int>(type: "integer", nullable: false),
                    DemotionZoneSize = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueSettings", x => x.Id);
                });

            // Seed the single settings row with the previously hardcoded defaults.
            migrationBuilder.Sql(@"
                INSERT INTO ""LeagueSettings"" (""Id"", ""MaximumLeagueParticipantCount"", ""PromotionZoneSize"", ""DemotionZoneSize"")
                VALUES (gen_random_uuid(), 30, 10, 5);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeagueSettings");
        }
    }
}
