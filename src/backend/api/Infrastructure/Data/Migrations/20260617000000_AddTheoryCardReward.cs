using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalesTrainer.Api.Infrastructure.Data;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260617000000_AddTheoryCardReward")]
    public partial class AddTheoryCardReward : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Theory cards award a small fixed XP, less than a practice exercise (10).
            // Submitted once at the end of a theory lesson, so this is the per-lesson reward.
            // Admin-editable afterwards via the ExerciseTypeRewards table.
            migrationBuilder.Sql(@"
                INSERT INTO ""ExerciseTypeRewards"" (""Id"", ""ExerciseType"", ""BaseXpReward"")
                VALUES (gen_random_uuid(), 'theory_card', 5)
                ON CONFLICT (""ExerciseType"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""ExerciseTypeRewards"" WHERE ""ExerciseType"" = 'theory_card';");
        }
    }
}
