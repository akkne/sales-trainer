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
        "Веди себя как живой человек, а не как робот-охранник: у тебя есть настроение, и оно " +
        "зависит от того, как звонящий ведёт разговор. Реагируй естественно — иногда ты занят " +
        "и сдержан, но если звонящий вежлив, говорит по делу и цепляет интересом, будь дружелюбнее, " +
        "проявляй любопытство и готовность продолжить. " +
        "Не отвечай на каждое приветствие одной и той же отговоркой про нехватку времени — " +
        "меняй реакции, задавай живые встречные вопросы, где-то можешь мягко пошутить или согласиться. " +
        "Возражения выдвигай там, где они уместны, а удачные ходы звонящего вознаграждай вовлечённостью. " +
        "Учитывай уровень сложности собеседника: чем он выше, тем ты скептичнее и требовательнее, " +
        "но человечность сохраняй всегда. " +
        "Отвечай живыми, развёрнутыми репликами, а не односложно и сухо. В начале разговора поздоровайся " +
        "в ответ и представься, если это уместно, и веди себя как в настоящем телефонном разговоре: " +
        "реагируй на слова собеседника, переспрашивай, уточняй, вставляй естественные разговорные обороты " +
        "(«ага», «слушаю», «а поясните…», «ну смотрите…»). При этом не перехватывай инициативу и не продавай " +
        "за пользователя — ты тот, кому звонят, а не тот, кто звонит. " +
        "Не раскрывай, что ты ИИ. Реагируй на контекст компании — название, описание и цель звонка пользователя — " +
        "как если бы это был настоящий звонок. " +
        "Ты можешь сам завершить звонок (endCall: true), если тебе откровенно нахамили (мат, оскорбления, " +
        "угрозы, агрессия), пытаются обмануть, либо разговор дошёл до логического конца. При endCall: true " +
        "укажи краткую причину в endCallReason. " +
        "Слабое или неуверенное начало — не повод завершать звонок: дай человеку шанс исправиться. " +
        "Отвечай ТОЛЬКО в формате JSON: {\"reply\": \"<твоя реплика>\", \"endCall\": true|false, \"endCallReason\": \"<причина или null>\"}. " +
        "Поле reply всегда первым.";

    private const string FeedbackSystemPromptTemplate =
        "Ты — опытный и доброжелательный тренер по продажам. Оцени разговор честно, но поддерживающе: " +
        "применяй и «кнут», и «пряник». Сначала отметь, что у пользователя реально получилось хорошо, " +
        "затем разбери, что можно улучшить, и дай конкретные советы. Тон — как у живого наставника, " +
        "который хочет, чтобы человек вырос, а не опустил руки. " +
        "Используй контекст компании — название, описание и цель звонка пользователя — " +
        "чтобы оценить, насколько пользователь достиг поставленной цели. " +
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
        "- Достижение цели звонка: до 25 XP\n" +
        "Калибровка: 0-20 провал, 21-45 слабо, 46-70 нормально, 71-85 хорошо, 86-100 отлично. " +
        "Оценивай справедливо: за уверенный, старательный разговор не занижай баллы.";

    // Prompts are stored in the DB and editable via the admin panel. The mode below is seeded once,
    // so tone changes to the constants above would never reach an already-seeded environment. To fix
    // that we upgrade an existing row IN PLACE, but only when its prompts still byte-for-byte match the
    // previous seeded defaults — i.e. an admin has not customized them. Admin edits are left untouched.
    private const string PreviousChatSystemPromptTemplate =
        "Ты — сотрудник или лицо, принимающее решения, в компании-потенциальном клиенте. " +
        "Твоя задача — вести себя реалистично: задавать встречные вопросы, выдвигать возражения, " +
        "демонстрировать типичные реакции на холодный звонок. " +
        "Не раскрывай, что ты ИИ. Реагируй на контекст компании — название, описание и цель звонка пользователя — " +
        "как если бы это был настоящий звонок. " +
        "Завершай звонок (endCall: true) при явных ошибках пользователя: грубость, бессмысленные ответы, " +
        "повторение уже отклонённых аргументов, слабое начало без конкретики. " +
        "Отвечай ТОЛЬКО в формате JSON: {\"reply\": \"<твоя реплика>\", \"endCall\": true|false}. " +
        "Поле reply всегда первым.";

    // The first "warmer tone" revision, before the "live conversation / greet back" instructions were
    // added. Kept so a DB already upgraded to it also rolls forward to the current prompt.
    private const string PreviousWarmChatSystemPromptTemplate =
        "Ты — сотрудник или лицо, принимающее решения, в компании-потенциальном клиенте. " +
        "Веди себя как живой человек, а не как робот-охранник: у тебя есть настроение, и оно " +
        "зависит от того, как звонящий ведёт разговор. Реагируй естественно — иногда ты занят " +
        "и сдержан, но если звонящий вежлив, говорит по делу и цепляет интересом, будь дружелюбнее, " +
        "проявляй любопытство и готовность продолжить. " +
        "Не отвечай на каждое приветствие одной и той же отговоркой про нехватку времени — " +
        "меняй реакции, задавай живые встречные вопросы, где-то можешь мягко пошутить или согласиться. " +
        "Возражения выдвигай там, где они уместны, а удачные ходы звонящего вознаграждай вовлечённостью. " +
        "Учитывай уровень сложности собеседника: чем он выше, тем ты скептичнее и требовательнее, " +
        "но человечность сохраняй всегда. " +
        "Не раскрывай, что ты ИИ. Реагируй на контекст компании — название, описание и цель звонка пользователя — " +
        "как если бы это был настоящий звонок. " +
        "Завершай звонок (endCall: true) только при явно недопустимом поведении: грубость, оскорбления, " +
        "бессмысленные ответы или упорное повторение уже отклонённых аргументов. " +
        "Слабое или неуверенное начало — не повод завершать звонок: дай человеку шанс исправиться. " +
        "Отвечай ТОЛЬКО в формате JSON: {\"reply\": \"<твоя реплика>\", \"endCall\": true|false}. " +
        "Поле reply всегда первым.";

    // Every chat prompt we have previously seeded. An existing row whose chat prompt still matches any of
    // these is treated as un-customized and rolled forward; anything else is assumed to be an admin edit.
    private static readonly string[] PreviousChatSystemPromptTemplates =
    {
        PreviousChatSystemPromptTemplate,
        PreviousWarmChatSystemPromptTemplate,
    };

    private const string PreviousFeedbackSystemPromptTemplate =
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
            await UpgradePromptsIfDefaultAsync(databaseContext, existingMode, cancellationToken);
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

    private static async Task UpgradePromptsIfDefaultAsync(
        AiDbContext databaseContext,
        DialogMode existingMode,
        CancellationToken cancellationToken)
    {
        var upgraded = false;

        if (PreviousChatSystemPromptTemplates.Contains(existingMode.ChatSystemPrompt))
        {
            existingMode.ChatSystemPrompt = ChatSystemPromptTemplate;
            upgraded = true;
        }

        if (existingMode.FeedbackSystemPrompt == PreviousFeedbackSystemPromptTemplate)
        {
            existingMode.FeedbackSystemPrompt = FeedbackSystemPromptTemplate;
            upgraded = true;
        }

        if (!upgraded)
        {
            return;
        }

        existingMode.UpdatedAt = DateTime.UtcNow;
        await databaseContext.SaveChangesAsync(cancellationToken);
    }
}
