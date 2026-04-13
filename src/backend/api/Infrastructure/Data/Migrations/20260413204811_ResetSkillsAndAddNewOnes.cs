using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResetSkillsAndAddNewOnes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete all related data first (order matters due to references)
            migrationBuilder.Sql("DELETE FROM \"DialogModes\"");
            migrationBuilder.Sql("DELETE FROM \"DialogBundles\"");
            migrationBuilder.Sql("DELETE FROM \"UserExerciseAttempts\"");
            migrationBuilder.Sql("DELETE FROM \"Exercises\"");
            migrationBuilder.Sql("DELETE FROM \"UserLessonProgressRecords\"");
            migrationBuilder.Sql("DELETE FROM \"Lessons\"");
            migrationBuilder.Sql("DELETE FROM \"ReferenceMaterials\"");
            migrationBuilder.Sql("DELETE FROM \"UserSkillProgressRecords\"");
            migrationBuilder.Sql("DELETE FROM \"Skills\"");

            // Insert new skills
            var skills = new[]
            {
                ("sale-prep", "Подготовка к продаже", "search", 1, false),
                ("first-contact", "Первый контакт", "phone-outgoing", 2, true),
                ("lead-qualification", "Квалификация лида", "user-check", 3, false),
                ("needs-discovery", "Выявление потребностей", "message-circle", 4, false),
                ("offer-packaging", "Упаковка оффера", "package", 5, false),
                ("presentation", "Презентация и демо", "presentation", 6, false),
                ("email-marketing", "Продающие рассылки", "mail", 7, false),
                ("content-sales", "Контент, который продаёт", "file-text", 8, false),
                ("objection-handling", "Работа с возражениями", "shield", 9, false),
                ("price-negotiation", "Переговоры о цене", "dollar-sign", 10, false),
                ("closing", "Закрытие сделки", "check-circle", 11, false),
                ("follow-up", "Follow-up и nurturing", "repeat", 12, false),
                ("upsell", "Расширение сделки", "trending-up", 13, false),
                ("pipeline", "Управление воронкой", "filter", 14, false)
            };

            foreach (var (slug, title, iconName, sortOrder, isDefault) in skills)
            {
                var id = Guid.NewGuid();
                migrationBuilder.Sql($@"
                    INSERT INTO ""Skills"" (""Id"", ""Slug"", ""Title"", ""IconName"", ""SortOrder"", ""PrerequisiteSkillId"", ""ApplicableSalesTypes"")
                    VALUES ('{id}', '{slug}', '{title}', '{iconName}', {sortOrder}, NULL, ARRAY['b2b', 'b2c', 'expert']::text[])
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This migration is destructive and cannot be reversed
            // The old data is permanently deleted
        }
    }
}
