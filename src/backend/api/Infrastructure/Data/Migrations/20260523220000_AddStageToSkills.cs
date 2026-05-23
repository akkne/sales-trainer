using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalesTrainer.Api.Infrastructure.Data;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260523220000_AddStageToSkills")]
    public partial class AddStageToSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "Skills",
                type: "text",
                nullable: false,
                defaultValue: "general");

            // Backfill stages for the 14 seeded skills. Buckets follow the sales funnel.
            migrationBuilder.Sql(@"
                UPDATE ""Skills"" SET ""Stage"" = 'preparation' WHERE ""IconicName"" IN ('sale-prep', 'first-contact', 'lead-qualification');
                UPDATE ""Skills"" SET ""Stage"" = 'discovery'   WHERE ""IconicName"" IN ('needs-discovery', 'offer-packaging', 'presentation');
                UPDATE ""Skills"" SET ""Stage"" = 'engagement'  WHERE ""IconicName"" IN ('email-marketing', 'content-sales');
                UPDATE ""Skills"" SET ""Stage"" = 'closing'     WHERE ""IconicName"" IN ('objection-handling', 'price-negotiation', 'closing');
                UPDATE ""Skills"" SET ""Stage"" = 'retention'   WHERE ""IconicName"" IN ('follow-up', 'upsell', 'pipeline');
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Stage",
                table: "Skills",
                column: "Stage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Skills_Stage",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "Skills");
        }
    }
}
