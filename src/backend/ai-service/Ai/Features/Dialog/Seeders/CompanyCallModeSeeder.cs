using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Features.Dialog.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Infrastructure.Data;

namespace Sellevate.Ai.Features.Dialog.Seeders;

public static class CompanyCallModeSeeder
{
    public static readonly Guid CompanyCallBundleId = new("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid CompanyCallModeId = new("a2000000-0000-0000-0000-000000000001");
    private static readonly Guid CompanyCallSkillId = new("a3000000-0000-0000-0000-000000000001");

    private const string BundleTitle = "Звонок по компании";
    private const string BundleDescription = "Скрытый пакет для практики холодных звонков по конкретной компании клиента.";
    private const string BundleIconEmoji = "🏢";

    private const string ModeTitle = "Холодный звонок";
    private const string ModeDescription = "ИИ играет роль сотрудника или лица, принимающего решения, в компании-потенциальном клиенте. Вы практикуете реальный сценарий холодного звонка с учётом конкретной компании и цели.";

    private const string ChatSystemPromptTemplate =
        "Ты — сотрудник или лицо, принимающее решения, в компании-потенциальном клиенте. " +
        "Твоя задача — вести себя реалистично: задавать встречные вопросы, выдвигать возражения, " +
        "демонстрировать типичные реакции на холодный звонок. " +
        "Не раскрывай, что ты ИИ. Реагируй на контекст компании — название, описание и цель звонка пользователя — " +
        "как если бы это был настоящий звонок. " +
        "Завершай звонок (endCall: true) при явных ошибках пользователя: грубость, бессмысленные ответы, " +
        "повторение уже отклонённых аргументов, слабое начало без конкретики. " +
        "Отвечай ТОЛЬКО в формате JSON: {\"reply\": \"<твоя реплика>\", \"endCall\": true|false}. " +
        "Поле reply всегда первым.";

    private const string FeedbackSystemPromptTemplate =
        "Ты — строгий тренер по продажам. Оцени разговор пользователя с потенциальным клиентом. " +
        "Используй контекст компании — название, описание и цель звонка пользователя — " +
        "чтобы оценить, насколько пользователь достиг поставленной цели. " +
        "Приводи цитаты из диалога. Не придумывай похвалу, которой не было. " +
        "Формат ответа:\n" +
        "[DETAILED]\n" +
        "<подробная обратная связь с разбором сильных и слабых сторон>\n" +
        "[SUMMARY]\n" +
        "<краткое резюме в 1-2 предложения>\n" +
        "[XP:<число от 0 до 100>]\n\n" +
        "Критерии XP (каждый считается только если реально был в диалоге):\n" +
        "- Уверенность и тон: до 25 XP\n" +
        "- Структура аргументов: до 25 XP\n" +
        "- Работа с возражениями (если были): до 25 XP\n" +
        "- Достижение цели звонка: до 25 XP\n" +
        "Калибровка: 0-20 провал, 21-45 слабо, 46-70 нормально, 71-85 хорошо, 86-100 исключительно (редко).";

    public static async Task SeedAsync(AiDbContext databaseContext, CancellationToken cancellationToken = default)
    {
        var existingMode = await databaseContext.DialogModes
            .FirstOrDefaultAsync(mode => mode.Key == DialogModeKeys.CompanyCall, cancellationToken);

        if (existingMode != null)
        {
            return;
        }

        var existingBundle = await databaseContext.DialogBundles
            .FirstOrDefaultAsync(bundle => bundle.Id == CompanyCallBundleId, cancellationToken);

        if (existingBundle == null)
        {
            var bundle = new DialogBundle
            {
                Id = CompanyCallBundleId,
                SkillId = CompanyCallSkillId,
                Title = BundleTitle,
                Description = BundleDescription,
                IconEmoji = BundleIconEmoji,
                SortOrder = 0,
                IsActive = true,
                IsHidden = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            databaseContext.DialogBundles.Add(bundle);
        }

        var mode = new DialogMode
        {
            Id = CompanyCallModeId,
            BundleId = CompanyCallBundleId,
            Key = DialogModeKeys.CompanyCall,
            Title = ModeTitle,
            Description = ModeDescription,
            ChatSystemPrompt = ChatSystemPromptTemplate,
            FeedbackSystemPrompt = FeedbackSystemPromptTemplate,
            SortOrder = 1,
            IsActive = true,
            VoiceEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        databaseContext.DialogModes.Add(mode);

        await databaseContext.SaveChangesAsync(cancellationToken);
    }
}
