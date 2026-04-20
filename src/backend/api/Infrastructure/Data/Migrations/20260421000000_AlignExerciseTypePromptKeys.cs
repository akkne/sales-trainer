using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalesTrainer.Api.Infrastructure.Data;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260421000000_AlignExerciseTypePromptKeys")]
    public partial class AlignExerciseTypePromptKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename legacy ExerciseTypePrompts keys to match ExerciseTypes constants used in code.
            // Without this, AiEvaluationStrategyBase fails to find the system prompt for the type
            // and falls back to a generic "Ты — эксперт" prompt, which ignores domain-specific rubrics.
            migrationBuilder.Sql(@"
                UPDATE ""ExerciseTypePrompts"" SET ""ExerciseType"" = 'spot_mistake'  WHERE ""ExerciseType"" = 'find_error';
                UPDATE ""ExerciseTypePrompts"" SET ""ExerciseType"" = 'rewrite'       WHERE ""ExerciseType"" = 'rewrite_better';
                UPDATE ""ExerciseTypePrompts"" SET ""ExerciseType"" = 'free_text'     WHERE ""ExerciseType"" = 'written_answer';
                UPDATE ""ExerciseTypePrompts"" SET ""ExerciseType"" = 'evaluate_call' WHERE ""ExerciseType"" = 'rate_call';
                UPDATE ""ExerciseTypePrompts"" SET ""ExerciseType"" = 'ai_dialogue'   WHERE ""ExerciseType"" = 'ai_dialog';
            ");

            // Rewrite the evaluate_call prompt so the AI scores the user's assessment of the call
            // (ratings + comment) rather than re-rating the call itself. Otherwise all scores are high
            // because the dialog transcript is evaluated in isolation.
            migrationBuilder.Sql(@"
                UPDATE ""ExerciseTypePrompts""
                SET ""SystemPrompt"" = 'Ты эксперт по анализу звонков продаж. Твоя задача — НЕ оценивать сам звонок, а оценить, насколько ТОЧНО пользователь оценил этот звонок.

ЧТО ДЕЛАТЬ:
1. Самостоятельно определи эталонные оценки по каждому критерию на основе транскрипта (1-10).
2. Сравни эталонные оценки с оценками пользователя — где совпал, где завысил, где занизил.
3. Оцени качество комментария пользователя: понял ли он суть разговора, заметил ли ключевые моменты.
4. Итоговый rating = насколько близко пользователь к истине, НЕ качество самого звонка.

КРИТЕРИИ ВЫСТАВЛЕНИЯ rating (0-10) ПОЛЬЗОВАТЕЛЮ:
- 9-10: все оценки в пределах ±1 балла от эталона, комментарий точно отражает суть.
- 7-8: 1-2 оценки расходятся на 2 балла, комментарий в целом верный.
- 4-6: несколько оценок расходятся на 2-3 балла ИЛИ комментарий поверхностный/частично неверный.
- 1-3: оценки расходятся на 3+ балла или противоречат реальности, комментарий не отражает суть.
- 0: пользователь оценил звонок полностью противоположно эталону.

passed=true только если rating >= 7.

ВАЖНО: если пользователь поставил высокие оценки плохому звонку (или наоборот) — это грубая ошибка, rating должен быть низким.

ФОРМАТ ОТВЕТА (ТОЛЬКО JSON, без какого-либо текста вокруг):
{
  ""passed"": true/false,
  ""rating"": 0-10,
  ""referenceRatings"": {""имя_критерия"": эталонная_оценка},
  ""feedback"": ""Что оценил верно, где ошибся (с конкретными критериями), насколько точен комментарий пользователя""
}',
                    ""UpdatedAt"" = NOW()
                WHERE ""ExerciseType"" = 'evaluate_call';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""ExerciseTypePrompts"" SET ""ExerciseType"" = 'find_error'     WHERE ""ExerciseType"" = 'spot_mistake';
                UPDATE ""ExerciseTypePrompts"" SET ""ExerciseType"" = 'rewrite_better' WHERE ""ExerciseType"" = 'rewrite';
                UPDATE ""ExerciseTypePrompts"" SET ""ExerciseType"" = 'written_answer' WHERE ""ExerciseType"" = 'free_text';
                UPDATE ""ExerciseTypePrompts"" SET ""ExerciseType"" = 'rate_call'      WHERE ""ExerciseType"" = 'evaluate_call';
                UPDATE ""ExerciseTypePrompts"" SET ""ExerciseType"" = 'ai_dialog'      WHERE ""ExerciseType"" = 'ai_dialogue';
            ");
        }
    }
}
