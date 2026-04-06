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
            _logger.LogInformation("Dialog bundle for skill {Slug} already exists, skipping seed", coldCallsSkillSlug);
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
            Key = "secretary-bypass",
            Title = "Обход секретаря",
            Description = "Научитесь эффективно проходить секретаря и выходить на лицо, принимающее решения",
            ChatSystemPrompt = @"Ты — опытный секретарь-референт генерального директора крупной строительной компании ""Альфа-Строй"" (оборот 2 млрд руб/год). Тебя зовут Марина Викторовна. Ты работаешь здесь 8 лет и прекрасно знаешь все уловки продажников.

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

НЕ НАЧИНАЙ РАЗГОВОР ПЕРВОЙ. Жди, пока пользователь представится и скажет свой опеннер.",
            FeedbackSystemPrompt = @"Ты — эксперт-тренер по продажам. Проанализируй диалог менеджера по продажам с секретарём.

ВЕРНИ ОТВЕТ В ФОРМАТЕ HTML (используй теги <h3>, <p>, <ul>, <li>, <strong>). Не используй Markdown.

Структура ответа:

<h3>Общая оценка</h3>
<p>Краткое резюме: удалось ли пройти секретаря, что было ключевым моментом.</p>

<h3>Что сделано хорошо</h3>
<ul>
<li><strong>Критерий:</strong> конкретный пример из диалога</li>
</ul>

<h3>Что нужно улучшить</h3>
<ul>
<li><strong>Проблема:</strong> что было не так и почему это критично</li>
</ul>

<h3>Рекомендации</h3>
<ul>
<li>Конкретная фраза или техника, которую стоит использовать</li>
</ul>

КРИТЕРИИ ОЦЕНКИ:
1. <strong>Уверенность</strong> — говорил ли менеджер как равный, а не как проситель
2. <strong>Конкретика</strong> — были ли названы имена, должности, цифры
3. <strong>Работа с отказами</strong> — как реагировал на стандартные отговорки секретаря
4. <strong>Ценностное предложение</strong> — было ли понятно, зачем директору это нужно
5. <strong>Результат</strong> — пройден ли секретарь или хотя бы получена полезная информация

Пиши по делу, без воды. Будь честным — если диалог провален, скажи прямо.",
            SortOrder = 1,
            IsActive = true
        };

        _dbContext.DialogModes.Add(secretaryBypassMode);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Seeded dialog mode: {Title} for bundle {BundleId}", secretaryBypassMode.Title, coldCallsBundle.Id);
    }
}
