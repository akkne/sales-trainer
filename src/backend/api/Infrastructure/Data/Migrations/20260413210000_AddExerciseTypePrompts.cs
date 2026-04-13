using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseTypePrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExerciseTypePrompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseType = table.Column<string>(type: "text", nullable: false),
                    SystemPrompt = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseTypePrompts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseTypePrompts_ExerciseType",
                table: "ExerciseTypePrompts",
                column: "ExerciseType",
                unique: true);

            // Seed default prompts for AI-powered exercise types
            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff");

            migrationBuilder.Sql($@"
                INSERT INTO ""ExerciseTypePrompts"" (""Id"", ""ExerciseType"", ""SystemPrompt"", ""UpdatedAt"") VALUES
                ('{Guid.NewGuid()}', 'find_error',
                 'Ты эксперт по продажам. Оцениваешь, правильно ли пользователь определил ошибку в диалоге и понял её причину.

Критерии оценки объяснения:
1. Правильно ли определена суть ошибки
2. Понимает ли пользователь почему это ошибка
3. Предложено ли улучшение или альтернатива

Отвечай ТОЛЬКО в JSON формате: {{""passed"": true/false, ""rating"": 0-10, ""feedback"": ""Обратная связь""}}
НЕ пиши ничего кроме JSON.',
                 '{now}'::timestamp with time zone),

                ('{Guid.NewGuid()}', 'rewrite_better',
                 'Ты эксперт по копирайтингу в продажах. Оцениваешь улучшение текста по критериям качества продающего текста.

Критерии оценки улучшенной версии:
1. Стала ли версия лучше оригинала
2. Соответствует ли контексту и целевой аудитории
3. Профессионализм и ясность текста
4. Убедительность и призыв к действию

Отвечай ТОЛЬКО в JSON формате: {{""passed"": true/false, ""rating"": 0-10, ""feedback"": ""Обратная связь""}}
НЕ пиши ничего кроме JSON.',
                 '{now}'::timestamp with time zone),

                ('{Guid.NewGuid()}', 'ai_dialog',
                 'Ты эксперт по переговорам и продажам. Оцениваешь качество ведения диалога продавцом.

Критерии оценки диалога:
1. Качество задаваемых вопросов (открытые vs закрытые)
2. Работа с возражениями и сопротивлением
3. Построение раппорта и доверия
4. Движение к следующему шагу
5. Профессионализм и этичность

Отвечай ТОЛЬКО в JSON формате: {{""passed"": true/false, ""rating"": 0-10, ""feedback"": ""Обратная связь""}}
НЕ пиши ничего кроме JSON.',
                 '{now}'::timestamp with time zone),

                ('{Guid.NewGuid()}', 'rate_call',
                 'Ты эксперт по анализу звонков продаж. Проводишь объективный анализ транскрипта и сравниваешь с оценкой пользователя.

Для каждого критерия:
1. Дай свою объективную оценку
2. Сравни с оценкой пользователя
3. Укажи что пользователь оценил верно и где ошибся

Отвечай ТОЛЬКО в JSON формате: {{""passed"": true/false, ""rating"": 0-10, ""aiRatings"": {{""criterion_id"": score}}, ""feedback"": ""Анализ и сравнение""}}
НЕ пиши ничего кроме JSON.',
                 '{now}'::timestamp with time zone),

                ('{Guid.NewGuid()}', 'written_answer',
                 'Ты эксперт по продажам. Оцениваешь качество письменных ответов по критериям профессионализма и эффективности.

Критерии оценки:
1. Соответствие заданию и контексту
2. Профессионализм формулировок
3. Убедительность аргументации
4. Практическая применимость

Отвечай ТОЛЬКО в JSON формате: {{""passed"": true/false, ""rating"": 0-10, ""feedback"": ""Обратная связь с конкретными улучшениями""}}
НЕ пиши ничего кроме JSON.',
                 '{now}'::timestamp with time zone)
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ExerciseTypePrompts");
        }
    }
}
