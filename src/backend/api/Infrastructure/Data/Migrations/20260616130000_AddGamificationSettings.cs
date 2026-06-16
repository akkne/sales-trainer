using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalesTrainer.Api.Infrastructure.Data;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260616130000_AddGamificationSettings")]
    public partial class AddGamificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Singleton tunables (daily/weekly XP goals, dialog scoring). Mirrors LeagueSettings.
            migrationBuilder.CreateTable(
                name: "GamificationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DailyXpGoal = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    WeeklyXpGoal = table.Column<int>(type: "integer", nullable: false, defaultValue: 500),
                    DialogXpMultiplier = table.Column<double>(type: "double precision", nullable: false, defaultValue: 1.0),
                    DialogWeightConfidence = table.Column<int>(type: "integer", nullable: false, defaultValue: 25),
                    DialogWeightStructure = table.Column<int>(type: "integer", nullable: false, defaultValue: 25),
                    DialogWeightObjection = table.Column<int>(type: "integer", nullable: false, defaultValue: 25),
                    DialogWeightGoal = table.Column<int>(type: "integer", nullable: false, defaultValue: 25)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GamificationSettings", x => x.Id);
                });

            // Per-exercise-type base XP (previously a hardcoded 10).
            migrationBuilder.CreateTable(
                name: "ExerciseTypeRewards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BaseXpReward = table.Column<int>(type: "integer", nullable: false, defaultValue: 10)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseTypeRewards", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseTypeRewards_ExerciseType",
                table: "ExerciseTypeRewards",
                column: "ExerciseType",
                unique: true);

            // Streak milestones (previously a hardcoded 7->50, 30->200 switch).
            migrationBuilder.CreateTable(
                name: "StreakMilestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DayCount = table.Column<int>(type: "integer", nullable: false),
                    XpReward = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreakMilestones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StreakMilestones_DayCount",
                table: "StreakMilestones",
                column: "DayCount",
                unique: true);

            // Seed the singleton settings row with the historic defaults.
            migrationBuilder.Sql(@"
                INSERT INTO ""GamificationSettings""
                    (""Id"", ""DailyXpGoal"", ""WeeklyXpGoal"", ""DialogXpMultiplier"",
                     ""DialogWeightConfidence"", ""DialogWeightStructure"", ""DialogWeightObjection"", ""DialogWeightGoal"")
                VALUES (gen_random_uuid(), 100, 500, 1.0, 25, 25, 25, 25);
            ");

            // Seed base XP for every known exercise type with the historic flat value of 10.
            migrationBuilder.Sql(@"
                INSERT INTO ""ExerciseTypeRewards"" (""Id"", ""ExerciseType"", ""BaseXpReward"") VALUES
                    (gen_random_uuid(), 'choose_option', 10),
                    (gen_random_uuid(), 'fill_blank',    10),
                    (gen_random_uuid(), 'reorder',       10),
                    (gen_random_uuid(), 'match_pairs',   10),
                    (gen_random_uuid(), 'categorize',    10),
                    (gen_random_uuid(), 'spot_mistake',  10),
                    (gen_random_uuid(), 'rewrite',       10),
                    (gen_random_uuid(), 'ai_dialogue',   10),
                    (gen_random_uuid(), 'evaluate_call', 10),
                    (gen_random_uuid(), 'free_text',     10);
            ");

            // Seed the previously hardcoded streak milestones.
            migrationBuilder.Sql(@"
                INSERT INTO ""StreakMilestones"" (""Id"", ""DayCount"", ""XpReward"") VALUES
                    (gen_random_uuid(), 7,  50),
                    (gen_random_uuid(), 30, 200);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "StreakMilestones");
            migrationBuilder.DropTable(name: "ExerciseTypeRewards");
            migrationBuilder.DropTable(name: "GamificationSettings");
        }
    }
}
