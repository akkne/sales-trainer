using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellevate.Gamification.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GA4: make Leagues(WeekStartDate, Tier) unique so concurrent rollover calls
            // cannot create duplicate next-week leagues for the same tier.
            migrationBuilder.DropIndex(
                name: "IX_Leagues_WeekStartDate_Tier",
                table: "Leagues");

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_WeekStartDate_Tier",
                table: "Leagues",
                columns: new[] { "WeekStartDate", "Tier" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Leagues_WeekStartDate_Tier",
                table: "Leagues");

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_WeekStartDate_Tier",
                table: "Leagues",
                columns: new[] { "WeekStartDate", "Tier" });
        }
    }
}
