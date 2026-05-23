using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalesTrainer.Api.Infrastructure.Data;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260523221500_BackfillSkillStages")]
    public partial class BackfillSkillStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Catch slugs the previous migration missed: the actual seeded iconicNames
            // diverged from the initial bucket map (sales-prep vs sale-prep, etc.).
            migrationBuilder.Sql(@"
                UPDATE ""Skills"" SET ""Stage"" = 'preparation' WHERE ""IconicName"" = 'sales-prep';
                UPDATE ""Skills"" SET ""Stage"" = 'discovery'   WHERE ""IconicName"" = 'presentation-demo';
                UPDATE ""Skills"" SET ""Stage"" = 'engagement'  WHERE ""IconicName"" IN ('sales-outreach', 'selling-content');
                UPDATE ""Skills"" SET ""Stage"" = 'retention'   WHERE ""IconicName"" IN ('upsell-crosssell', 'pipeline-management');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Skills"" SET ""Stage"" = 'general' WHERE ""IconicName"" IN
                    ('sales-prep', 'presentation-demo', 'sales-outreach', 'selling-content',
                     'upsell-crosssell', 'pipeline-management');
            ");
        }
    }
}
