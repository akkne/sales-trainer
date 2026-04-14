using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorSkillsTopicsLessonsExercises : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First clear dependent data in the correct order
            migrationBuilder.Sql("DELETE FROM \"UserExerciseAttempts\"");
            migrationBuilder.Sql("DELETE FROM \"Exercises\"");
            migrationBuilder.Sql("DELETE FROM \"UserLessonProgressRecords\"");
            migrationBuilder.Sql("DELETE FROM \"Lessons\"");
            migrationBuilder.Sql("DELETE FROM \"UserSkillProgressRecords\"");
            migrationBuilder.Sql("DELETE FROM \"ReferenceMaterials\"");
            migrationBuilder.Sql("DELETE FROM \"DialogModes\"");
            migrationBuilder.Sql("DELETE FROM \"DialogBundles\"");
            migrationBuilder.Sql("DELETE FROM \"Skills\"");

            // Drop old columns from Skills
            migrationBuilder.DropColumn(name: "ApplicableSalesTypes", table: "Skills");
            migrationBuilder.DropColumn(name: "IconName", table: "Skills");
            migrationBuilder.DropColumn(name: "PrerequisiteSkillId", table: "Skills");
            migrationBuilder.DropColumn(name: "Slug", table: "Skills");

            // Rename SortOrder to OrderInTree for Skills
            migrationBuilder.RenameColumn(name: "SortOrder", table: "Skills", newName: "OrderInTree");

            // Add Description column to Skills
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Skills",
                type: "text",
                nullable: true);

            // Create Topics table
            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderInSkill = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Topics_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Topics_SkillId_OrderInSkill",
                table: "Topics",
                columns: new[] { "SkillId", "OrderInSkill" });

            // Drop old columns from Lessons
            migrationBuilder.DropColumn(name: "Description", table: "Lessons");
            migrationBuilder.DropColumn(name: "DifficultyLevel", table: "Lessons");
            migrationBuilder.DropColumn(name: "EstimatedMinutes", table: "Lessons");
            migrationBuilder.DropColumn(name: "SortOrder", table: "Lessons");
            migrationBuilder.DropColumn(name: "XpReward", table: "Lessons");
            migrationBuilder.DropColumn(name: "SkillId", table: "Lessons");

            // Add new columns to Lessons
            migrationBuilder.AddColumn<Guid>(
                name: "TopicId",
                table: "Lessons",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<int>(
                name: "OrderInTopic",
                table: "Lessons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_TopicId_OrderInTopic",
                table: "Lessons",
                columns: new[] { "TopicId", "OrderInTopic" });

            migrationBuilder.AddForeignKey(
                name: "FK_Lessons_Topics_TopicId",
                table: "Lessons",
                column: "TopicId",
                principalTable: "Topics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Update Exercises table
            migrationBuilder.RenameColumn(name: "SortOrder", table: "Exercises", newName: "OrderInLesson");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Exercises",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<string>(
                name: "CustomAiPrompt",
                table: "Exercises",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Exercises",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_LessonId_OrderInLesson",
                table: "Exercises",
                columns: new[] { "LessonId", "OrderInLesson" });

            migrationBuilder.AddForeignKey(
                name: "FK_Exercises_Lessons_LessonId",
                table: "Exercises",
                column: "LessonId",
                principalTable: "Lessons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Exercises_Lessons_LessonId", table: "Exercises");
            migrationBuilder.DropForeignKey(name: "FK_Lessons_Topics_TopicId", table: "Lessons");
            migrationBuilder.DropTable(name: "Topics");
            migrationBuilder.DropIndex(name: "IX_Lessons_TopicId_OrderInTopic", table: "Lessons");
            migrationBuilder.DropIndex(name: "IX_Exercises_LessonId_OrderInLesson", table: "Exercises");
            migrationBuilder.DropColumn(name: "Description", table: "Skills");
            migrationBuilder.DropColumn(name: "CreatedAt", table: "Exercises");
            migrationBuilder.DropColumn(name: "CustomAiPrompt", table: "Exercises");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "Exercises");
            migrationBuilder.DropColumn(name: "TopicId", table: "Lessons");
            migrationBuilder.DropColumn(name: "OrderInTopic", table: "Lessons");

            migrationBuilder.RenameColumn(name: "OrderInTree", table: "Skills", newName: "SortOrder");
            migrationBuilder.RenameColumn(name: "OrderInLesson", table: "Exercises", newName: "SortOrder");

            migrationBuilder.AddColumn<string[]>(name: "ApplicableSalesTypes", table: "Skills", type: "text[]", nullable: false, defaultValue: new string[0]);
            migrationBuilder.AddColumn<string>(name: "IconName", table: "Skills", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<Guid>(name: "PrerequisiteSkillId", table: "Skills", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Slug", table: "Skills", type: "text", nullable: false, defaultValue: "");

            migrationBuilder.AddColumn<Guid>(name: "SkillId", table: "Lessons", type: "uuid", nullable: false, defaultValue: Guid.Empty);
            migrationBuilder.AddColumn<string>(name: "Description", table: "Lessons", type: "text", nullable: true);
            migrationBuilder.AddColumn<int>(name: "DifficultyLevel", table: "Lessons", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "EstimatedMinutes", table: "Lessons", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "SortOrder", table: "Lessons", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "XpReward", table: "Lessons", type: "integer", nullable: false, defaultValue: 0);
        }
    }
}
