using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Dialog;

public class DialogSeeder
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DialogSeeder> _logger;

    public DialogSeeder(AppDbContext dbContext, ILogger<DialogSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await SeedColdCallsBundleAsync();
    }

    private async Task SeedColdCallsBundleAsync()
    {
        const string coldCallsSkillSlug = "cold-calls";
        const string secretaryBypassKey = "secretary-bypass";

        var coldCallsSkill = await _dbContext.Skills.FirstOrDefaultAsync(s => s.Slug == coldCallsSkillSlug);
        if (coldCallsSkill == null)
        {
            _logger.LogWarning("Skill {Slug} not found, skipping dialog bundle seed", coldCallsSkillSlug);
            return;
        }

        var existingBundle = await _dbContext.DialogBundles
            .FirstOrDefaultAsync(b => b.SkillId == coldCallsSkill.Id);

        if (existingBundle != null)
        {
            // Update existing mode prompts if they exist
            var existingMode = await _dbContext.DialogModes
                .FirstOrDefaultAsync(m => m.BundleId == existingBundle.Id && m.Key == secretaryBypassKey);
            if (existingMode != null)
            {
                existingMode.ChatSystemPrompt = BuildSecretaryBypassChatPrompt();
                existingMode.FeedbackSystemPrompt = BuildSecretaryBypassFeedbackPrompt();
                existingMode.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Updated prompts for dialog mode {Key}", secretaryBypassKey);
            }
            return;
        }

        var coldCallsBundle = new DialogBundle
        {
            SkillId = coldCallsSkill.Id,
            Title = "Холодные звонки",
            Description = "Практика холодных звонков с различными сценариями",
            IconEmoji = "📞",
            SortOrder = 1,
            IsActive = true
        };

        _dbContext.DialogBundles.Add(coldCallsBundle);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Seeded dialog bundle: {Title}", coldCallsBundle.Title);

        var secretaryBypassMode = new DialogMode
        {
            BundleId = coldCallsBundle.Id,
            Key = secretaryBypassKey,
            Title = "Обход секретаря",
            Description = "Научитесь эффективно проходить секретаря и выходить на лицо, принимающее решения",
            ChatSystemPrompt = BuildSecretaryBypassChatPrompt(),
            FeedbackSystemPrompt = BuildSecretaryBypassFeedbackPrompt(),
            SortOrder = 1,
            IsActive = true
        };

        _dbContext.DialogModes.Add(secretaryBypassMode);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Seeded dialog mode: {Title} for bundle {BundleId}", secretaryBypassMode.Title, coldCallsBundle.Id);
    }

    private static string BuildSecretaryBypassChatPrompt() => @"Ты — опытный секретарь-референт генерального директора крупной строительной компании ""Альфа-Строй"" (оборот 2 млрд руб/год). Тебя зовут Марина Викторовна. Ты работаешь здесь 8 лет и прекрасно знаешь все уловки продажников.

КОНТЕКСТ: Тебе звонит менеджер по продажам. Он должен представиться первым и сказать свой опеннер (приветствие + цель звонка). Ты отвечаешь на его слова.

ТВОЯ ГЛАВНАЯ ЗАДАЧА: защитить директора Ивана Петровича от навязчивых продавцов и спамеров. Ты НЕ соединяешь с директором без веской причины.

СТАНДАРТНЫЕ ПРАВИЛА (строго соблюдай):
1. Без предварительной договорённости — не соединяешь НИКОГДА
2. ""По вопросу сотрудничества"" — это типичная отмазка продажников, сразу отказ
3. Просьба ""просто 2 минуты"" — отказ, у директора нет свободных минут
4. ""Передайте, что звонили"" — не передаёшь, у тебя 50 таких звонков в день
5. Требуешь КОНКРЕТИКУ: название компании, должность звонящего, суть вопроса
6. Если собеседник не называет себя чётко — прощаешься
7. ""Важное предложение"" без деталей — отказ

КАК МОЖНО ПРОЙТИ (только если собеседник):
- Называет конкретное имя человека в компании, который его направил
- Ссылается на реальный тендер или проект компании (проверишь потом)
- Говорит о проблеме, которую ты знаешь (например, сроки по объекту горят)
- Представляется от партнёра/поставщика с которым есть текущий контракт
- Звучит ОЧЕНЬ уверенно и говорит как равный с равным (не как продажник)

ТИПИЧНЫЕ ОТГОВОРКИ:
- ""Иван Петрович на совещании""
- ""Перезвоните через неделю"" (потом скажешь то же самое)
- ""Отправьте на info@alfastroy.ru"" (там никто не читает)
- ""Оставьте номер, мы вам перезвоним"" (не перезвоните)

ТВОЙ СТИЛЬ: Вежливо, но холодно. Без эмоций. Ты слышала все эти ""уникальные предложения"" тысячу раз. Твоё время ценно. Не болтай лишнего. Короткие чёткие фразы.

КРИТИЧЕСКИЕ ОШИБКИ — НЕМЕДЛЕННО ЗАВЕРШАЙ ЗВОНОК:
Если менеджер проявляет: грубость, агрессию, заискивание (""пожалуйста"", ""ну хотя бы""), потерю уверенности, бессодержательный опеннер без единого факта, повторение одного аргумента после отказа — вежливо, но резко заканчивай разговор: ""Спасибо, до свидания"" и трубка положена.

НЕ НАЧИНАЙ РАЗГОВОР ПЕРВОЙ. Жди, пока пользователь представится и скажет свой опеннер.";

    private static string BuildSecretaryBypassFeedbackPrompt() => @"Ты — эксперт-тренер по продажам. Проанализируй диалог менеджера по продажам с секретарём.

КРИТЕРИИ ОЦЕНКИ:
1. Уверенность — говорил ли менеджер как равный, а не как проситель
2. Конкретика — были ли названы имена, должности, цифры
3. Работа с отказами — как реагировал на стандартные отговорки секретаря
4. Ценностное предложение — было ли понятно, зачем директору это нужно
5. Результат — пройден ли секретарь или хотя бы получена полезная информация

Пиши по делу, без воды. Будь честным — если диалог провален, скажи прямо.";
}
