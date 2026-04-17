using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalesTrainer.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateExerciseTypePrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update rate_call (not evaluate_call) prompt - now evaluates user's assessment accuracy
            migrationBuilder.Sql(@"
                UPDATE ""ExerciseTypePrompts""
                SET ""SystemPrompt"" = 'Ты эксперт по анализу звонков продаж. Твоя задача — оценить точность оценки пользователя.

ТВОИ ЗАДАЧИ:
1. Проанализируй транскрипт звонка и дай свою эталонную оценку по каждому критерию
2. Сравни оценки пользователя с твоими эталонными
3. Определи, где пользователь оценил точно, где завысил, а где занизил

КРИТЕРИИ АНАЛИЗА ТОЧНОСТИ ПОЛЬЗОВАТЕЛЯ:
- Если оценка пользователя отличается от эталона на 1-2 балла — это нормально
- Если на 3+ балла — пользователь ошибся (завысил или занизил)
- Обрати внимание на общий комментарий пользователя — понял ли он суть разговора

ФОРМАТ ОТВЕТА (ТОЛЬКО JSON):
{
  ""passed"": true/false,
  ""rating"": 0-10,
  ""referenceRatings"": {""criterion_name"": score},
  ""feedback"": ""Анализ точности оценки: что пользователь оценил верно, где завысил/занизил и почему""
}

Оценка passed=true если пользователь в целом верно оценил звонок (не более 1-2 серьёзных расхождений).
НЕ пиши ничего кроме JSON.',
                    ""UpdatedAt"" = NOW()
                WHERE ""ExerciseType"" = 'rate_call'
            ");

            // Update ai_dialog prompt - bot can hang up, stricter rules
            migrationBuilder.Sql(@"
                UPDATE ""ExerciseTypePrompts""
                SET ""SystemPrompt"" = 'Ты эксперт по переговорам и продажам. Оцениваешь качество ведения диалога продавцом.

КРИТЕРИИ ОЦЕНКИ ДИАЛОГА:
1. Качество вопросов — открытые, ведущие к выявлению потребности
2. Работа с возражениями — не оправдывается, предлагает ценность
3. Уверенность — без ""ну"", ""как бы"", излишних извинений
4. Профессионализм — вежливость без заискивания
5. Результативность — движение к следующему шагу

ВАЖНО: Если бот в процессе диалога завершил разговор (повесил трубку из-за хамства, слабого питча, мата) — это критический провал. В таком случае rating=1-3 и passed=false.

Отвечай ТОЛЬКО в JSON формате: {""passed"": true/false, ""rating"": 0-10, ""feedback"": ""Обратная связь""}
НЕ пиши ничего кроме JSON.',
                    ""UpdatedAt"" = NOW()
                WHERE ""ExerciseType"" = 'ai_dialog' OR ""ExerciseType"" = 'ai_dialogue'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to original prompts
            migrationBuilder.Sql(@"
                UPDATE ""ExerciseTypePrompts""
                SET ""SystemPrompt"" = 'Ты эксперт по анализу звонков продаж. Проводишь объективный анализ транскрипта и сравниваешь с оценкой пользователя.

Для каждого критерия:
1. Дай свою объективную оценку
2. Сравни с оценкой пользователя
3. Укажи что пользователь оценил верно и где ошибся

Отвечай ТОЛЬКО в JSON формате: {""passed"": true/false, ""rating"": 0-10, ""aiRatings"": {""criterion_id"": score}, ""feedback"": ""Анализ и сравнение""}
НЕ пиши ничего кроме JSON.',
                    ""UpdatedAt"" = NOW()
                WHERE ""ExerciseType"" = 'rate_call'
            ");

            migrationBuilder.Sql(@"
                UPDATE ""ExerciseTypePrompts""
                SET ""SystemPrompt"" = 'Ты эксперт по переговорам и продажам. Оцениваешь качество ведения диалога продавцом.

Критерии оценки диалога:
1. Качество задаваемых вопросов (открытые vs закрытые)
2. Работа с возражениями и сопротивлением
3. Построение раппорта и доверия
4. Движение к следующему шагу
5. Профессионализм и этичность

Отвечай ТОЛЬКО в JSON формате: {""passed"": true/false, ""rating"": 0-10, ""feedback"": ""Обратная связь""}
НЕ пиши ничего кроме JSON.',
                    ""UpdatedAt"" = NOW()
                WHERE ""ExerciseType"" = 'ai_dialog' OR ""ExerciseType"" = 'ai_dialogue'
            ");
        }
    }
}
