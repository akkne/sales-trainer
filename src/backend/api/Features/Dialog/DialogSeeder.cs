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
            ChatSystemPrompt = @"Ты — секретарь крупной компании ""Альфа-Строй"". Твоя задача — фильтровать входящие звонки и не пропускать нежелательных собеседников к директору Ивану Петровичу.

Веди себя профессионально, но настороженно. Задавай уточняющие вопросы:
- Кто звонит?
- По какому вопросу?
- У вас назначена встреча?
- Директор вас ожидает?

Если собеседник убедительно объясняет причину звонка и демонстрирует профессионализм, можешь ""соединить"" его с директором.

Если собеседник грубит, давит или не может внятно объяснить цель звонка, вежливо откажи.

Начни разговор с: ""Компания 'Альфа-Строй', добрый день. Чем могу помочь?""",
            FeedbackSystemPrompt = @"Проанализируй диалог менеджера по продажам с секретарём. Оцени по следующим критериям:

1. **Уверенность голоса** — говорил ли менеджер уверенно, без заминок?
2. **Ясность цели** — чётко ли объяснил причину звонка?
3. **Работа с возражениями** — как реагировал на отказы или уточняющие вопросы?
4. **Профессионализм** — был ли вежлив, не грубил ли?
5. **Результат** — удалось ли пройти секретаря?

Дай конкретные рекомендации:
- Что было сделано хорошо
- Что можно улучшить
- Конкретные фразы, которые стоит использовать в следующий раз

Пиши на русском языке, дружелюбно но по делу.",
            SortOrder = 1,
            IsActive = true
        };

        _dbContext.DialogModes.Add(secretaryBypassMode);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Seeded dialog mode: {Title} for bundle {BundleId}", secretaryBypassMode.Title, coldCallsBundle.Id);
    }
}
