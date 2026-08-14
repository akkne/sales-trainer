using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Features.Dialog.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Infrastructure.Data;

namespace Sellevate.Ai.Features.Dialog.Seeders;

/// <summary>
/// Seeds the hidden bundle/mode pair behind "Кастомный сценарий" on the practice page.
/// </summary>
/// <remarks>
/// Same shape as <see cref="CompanyCallModeSeeder"/>: the bundle is hidden so it never appears in
/// the normal bundle list, and the client reaches it through GET /dialog/custom-scenario-mode. The
/// prompts here are deliberately generic — the specifics come from the user's scenario, spliced in
/// by <see cref="Helpers.CustomScenarioPromptBuilder"/>.
/// </remarks>
public static class CustomScenarioModeSeeder
{
    public static readonly Guid CustomScenarioBundleId = new("a1000000-0000-0000-0000-000000000002");
    public static readonly Guid CustomScenarioModeId = new("a2000000-0000-0000-0000-000000000002");
    private static readonly Guid CustomScenarioSkillId = new("a3000000-0000-0000-0000-000000000002");

    private const string BundleTitle = "Кастомный сценарий";
    private const string BundleDescription = "Скрытый пакет для практики по сценарию, который пользователь описывает сам.";
    private const string BundleIconEmoji = "✳️";

    private const string ModeTitle = "Свой сценарий";
    private const string ModeDescription = "ИИ отыгрывает собеседника по сценарию, который вы описали сами: своя ситуация, свой клиент, свой продукт.";

    private const string ChatSystemPromptTemplate =
        "Ты — собеседник в тренировочном разговоре о продажах. Роль, ситуация и предмет разговора " +
        "заданы сценарием пользователя ниже — играй строго по нему. " +
        "Веди себя как живой человек: у тебя есть настроение, интересы и своя выгода. Реагируй на то, " +
        "как пользователь ведёт разговор, — на вежливость и конкретику отвечай вовлечённостью, на " +
        "давление и пустые формулировки — сопротивлением. " +
        "Выдвигай возражения там, где они уместны для твоей роли, задавай встречные вопросы, " +
        "уточняй детали. Отвечай живыми, развёрнутыми репликами, а не односложно. " +
        "Не перехватывай инициативу и не продавай за пользователя — продаёт он, а не ты. " +
        "Не раскрывай, что ты ИИ, и не обсуждай сам сценарий как текст — просто живи в нём. " +
        "Если в сценарии чего-то не хватает, додумай недостающие детали правдоподобно и держись их " +
        "до конца разговора. " +
        "Ты можешь сам завершить разговор (endCall: true), если тебе откровенно нахамили (мат, " +
        "оскорбления, угрозы, агрессия), пытаются обмануть, либо разговор дошёл до логического конца. " +
        "При endCall: true укажи краткую причину в endCallReason. " +
        "Слабое или неуверенное начало — не повод завершать разговор: дай человеку шанс исправиться. " +
        "Отвечай ТОЛЬКО в формате JSON: {\"reply\": \"<твоя реплика>\", \"endCall\": true|false, \"endCallReason\": \"<причина или null>\"}. " +
        "Поле reply всегда первым.";

    private const string FeedbackSystemPromptTemplate =
        "Ты — опытный и доброжелательный тренер по продажам. Оцени разговор честно, но поддерживающе: " +
        "применяй и «кнут», и «пряник». Сначала отметь, что у пользователя реально получилось хорошо, " +
        "затем разбери, что можно улучшить, и дай конкретные советы. Тон — как у живого наставника, " +
        "который хочет, чтобы человек вырос, а не опустил руки. " +
        "Разговор шёл по сценарию, который пользователь придумал сам, — оценивай, насколько он " +
        "справился именно с этой ситуацией, а не с абстрактной продажей. " +
        "Приводи цитаты из диалога. Хвали только за то, что действительно было, но и не занижай реальные сильные стороны. " +
        "Формат ответа:\n" +
        "[DETAILED]\n" +
        "<подробная обратная связь: сначала сильные стороны, затем зоны роста с конкретными рекомендациями>\n" +
        "[SUMMARY]\n" +
        "<краткое ободряющее резюме в 1-2 предложения>\n" +
        "[XP:<число от 0 до 100>]\n\n" +
        "Критерии XP (каждый считается только если реально был в диалоге):\n" +
        "- Уверенность и тон: до 25 XP\n" +
        "- Структура аргументов: до 25 XP\n" +
        "- Работа с возражениями (если были): до 25 XP\n" +
        "- Продвижение к цели разговора: до 25 XP\n" +
        "Калибровка: 0-20 провал, 21-45 слабо, 46-70 нормально, 71-85 хорошо, 86-100 отлично. " +
        "Оценивай справедливо: за уверенный, старательный разговор не занижай баллы.";

    public static async Task SeedAsync(AiDbContext databaseContext, CancellationToken cancellationToken = default)
    {
        var alreadySeeded = await databaseContext.DialogModes
            .AnyAsync(mode => mode.Key == DialogModeKeys.CustomScenario, cancellationToken);

        if (alreadySeeded)
        {
            return;
        }

        var existingBundle = await databaseContext.DialogBundles
            .FirstOrDefaultAsync(bundle => bundle.Id == CustomScenarioBundleId, cancellationToken);

        if (existingBundle == null)
        {
            databaseContext.DialogBundles.Add(new DialogBundle
            {
                Id = CustomScenarioBundleId,
                SkillId = CustomScenarioSkillId,
                Title = BundleTitle,
                Description = BundleDescription,
                IconEmoji = BundleIconEmoji,
                SortOrder = 0,
                IsActive = true,
                IsHidden = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        databaseContext.DialogModes.Add(new DialogMode
        {
            Id = CustomScenarioModeId,
            BundleId = CustomScenarioBundleId,
            Key = DialogModeKeys.CustomScenario,
            Title = ModeTitle,
            Description = ModeDescription,
            ChatSystemPrompt = ChatSystemPromptTemplate,
            FeedbackSystemPrompt = FeedbackSystemPromptTemplate,
            SortOrder = 1,
            IsActive = true,
            VoiceEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await databaseContext.SaveChangesAsync(cancellationToken);
    }
}
