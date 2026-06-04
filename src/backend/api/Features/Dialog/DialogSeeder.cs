using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Dialog.Models;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Dialog;

public sealed class DialogSeeder(AppDbContext databaseContext, ILogger<DialogSeeder> logger)
{
    private sealed record SeedMode(
        string Key,
        string Title,
        string Description,
        string ChatSystemPrompt,
        string FeedbackSystemPrompt,
        int SortOrder,
        bool VoiceEnabled,
        string? VoiceId = null);

    private sealed record SeedBundle(
        string SkillIconicName,
        string Title,
        string Description,
        string IconEmoji,
        int SortOrder,
        IReadOnlyList<SeedMode> Modes);

    private static readonly IReadOnlyList<SeedBundle> DefaultBundles =
    [
        new SeedBundle(
            SkillIconicName: "cold-calls",
            Title: "Холодные звонки",
            Description: "Практика звонков незнакомым лицам, принимающим решение.",
            IconEmoji: "📞",
            SortOrder: 1,
            Modes:
            [
                new SeedMode(
                    Key: "secretary-bypass",
                    Title: "Обход секретаря",
                    Description: "Прорваться к ЛПР через занятого секретаря.",
                    ChatSystemPrompt:
                        "Ты — Анна, опытный секретарь генерального директора крупной торговой компании. " +
                        "Тебе постоянно звонят менеджеры по продажам и пытаются «прорваться» к директору. " +
                        "Твоя задача — фильтровать звонки и не пускать к директору тех, кто не может внятно объяснить, зачем нужен. " +
                        "Веди разговор естественно, как настоящий человек: будь занятой, не очень дружелюбной, " +
                        "но вежливой. Задавай уточняющие вопросы. Если менеджер уверен и конкретен — можешь смягчиться. " +
                        "Если мямлит, давит или продаёт «в лоб» — отказывай и завершай разговор.",
                    FeedbackSystemPrompt:
                        "Ты — опытный коуч по продажам. Оцени, насколько хорошо менеджер прошёл секретаря: " +
                        "уверенность тона, чёткость опеннера, работа с фильтрами, достижение цели (соединили с ЛПР или нет).",
                    SortOrder: 1,
                    VoiceEnabled: true),
                new SeedMode(
                    Key: "decision-maker-opener",
                    Title: "Опеннер на ЛПР",
                    Description: "Первые 30 секунд разговора с лицом, принимающим решение.",
                    ChatSystemPrompt:
                        "Ты — Сергей, коммерческий директор средней B2B-компании (производство упаковки). " +
                        "Тебе позвонил незнакомый менеджер по продажам. Времени у тебя мало, и в день звонит человек 5–10. " +
                        "Ты по умолчанию скептичен. Если опеннер слабый и без конкретики — вежливо отказываешь. " +
                        "Если менеджер сразу обозначает ценность и причину звонка — можешь продолжить разговор и задать встречные вопросы. " +
                        "Реагируй как живой человек, не помогай менеджеру.",
                    FeedbackSystemPrompt:
                        "Оцени опеннер менеджера: краткость, конкретику, формулировку ценности для собеседника, " +
                        "уверенность и работу с первичным сопротивлением.",
                    SortOrder: 2,
                    VoiceEnabled: true),
            ]),
        new SeedBundle(
            SkillIconicName: "objection-handling",
            Title: "Работа с возражениями",
            Description: "Отработка типичных «дорого», «нам не нужно», «уже работаем с другими».",
            IconEmoji: "🛡️",
            SortOrder: 2,
            Modes:
            [
                new SeedMode(
                    Key: "expensive-objection",
                    Title: "Возражение «Дорого»",
                    Description: "Клиент говорит, что цена слишком высокая.",
                    ChatSystemPrompt:
                        "Ты — Михаил, владелец розничного магазина. Менеджер уже презентовал тебе продукт, " +
                        "и ты сразу сказал «дорого». Ты не уходишь сразу — даёшь менеджеру шанс ответить на возражение. " +
                        "Если он умеет квалифицировать (сравнение, ROI, варианты комплектации) — продолжай разговор. " +
                        "Если просто оправдывается или сразу даёт скидку — оставайся на «дорого» и завершай разговор.",
                    FeedbackSystemPrompt:
                        "Оцени работу с возражением «дорого»: квалификация возражения, аргументация ценности, " +
                        "избегание скидок и оправданий, движение к следующему шагу.",
                    SortOrder: 1,
                    VoiceEnabled: true),
            ]),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var bundleCount = await databaseContext.DialogBundles.CountAsync(cancellationToken);
        if (bundleCount > 0)
        {
            logger.LogInformation("Dialog seed: {Count} bundles already present, skipping", bundleCount);
            return;
        }

        // Ensure a fallback skill exists for each bundle. If the named iconic
        // skill is missing, we lazily create a lightweight one so the seed
        // never blocks startup on missing skill data.
        foreach (var seedBundle in DefaultBundles)
        {
            var skill = await databaseContext.Skills
                .FirstOrDefaultAsync(s => s.IconicName == seedBundle.SkillIconicName, cancellationToken);

            if (skill == null)
            {
                skill = new Skill
                {
                    Id = Guid.NewGuid(),
                    IconicName = seedBundle.SkillIconicName,
                    Title = seedBundle.Title,
                    Description = seedBundle.Description,
                    OrderInTree = 100 + seedBundle.SortOrder,
                    Stage = "practice",
                };
                databaseContext.Skills.Add(skill);
                await databaseContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Dialog seed: created fallback skill {Iconic}", skill.IconicName);
            }

            var bundle = new DialogBundle
            {
                Id = Guid.NewGuid(),
                SkillId = skill.Id,
                Title = seedBundle.Title,
                Description = seedBundle.Description,
                IconEmoji = seedBundle.IconEmoji,
                SortOrder = seedBundle.SortOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            databaseContext.DialogBundles.Add(bundle);

            foreach (var seedMode in seedBundle.Modes)
            {
                databaseContext.DialogModes.Add(new DialogMode
                {
                    Id = Guid.NewGuid(),
                    BundleId = bundle.Id,
                    Key = seedMode.Key,
                    Title = seedMode.Title,
                    Description = seedMode.Description,
                    ChatSystemPrompt = seedMode.ChatSystemPrompt,
                    FeedbackSystemPrompt = seedMode.FeedbackSystemPrompt,
                    SortOrder = seedMode.SortOrder,
                    IsActive = true,
                    VoiceEnabled = seedMode.VoiceEnabled,
                    VoiceId = seedMode.VoiceId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Dialog seed: inserted {BundleCount} bundles with modes", DefaultBundles.Count);
    }
}
