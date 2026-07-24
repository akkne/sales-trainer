using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Sellevate.Ai.Features.Dialog.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Seeders;
using Sellevate.Ai.Infrastructure.Data;

namespace Sellevate.Ai.Tests.Unit;

[TestFixture]
public sealed class CompanyCallModeSeederTests
{
    // Byte-for-byte copies of the prompts seeded before the "warmer tone" change. The seeder upgrades
    // a row in place only when it still holds these exact strings (i.e. no admin has customized them).
    private const string PreviousChatSystemPrompt =
        "Ты — сотрудник или лицо, принимающее решения, в компании-потенциальном клиенте. " +
        "Твоя задача — вести себя реалистично: задавать встречные вопросы, выдвигать возражения, " +
        "демонстрировать типичные реакции на холодный звонок. " +
        "Не раскрывай, что ты ИИ. Реагируй на контекст компании — название, описание и цель звонка пользователя — " +
        "как если бы это был настоящий звонок. " +
        "Завершай звонок (endCall: true) при явных ошибках пользователя: грубость, бессмысленные ответы, " +
        "повторение уже отклонённых аргументов, слабое начало без конкретики. " +
        "Отвечай ТОЛЬКО в формате JSON: {\"reply\": \"<твоя реплика>\", \"endCall\": true|false}. " +
        "Поле reply всегда первым.";

    private const string PreviousFeedbackSystemPrompt =
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

    private static AiDbContext CreateInMemory() =>
        new(new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);

    private static async Task<DialogMode> GetSeededModeAsync(AiDbContext db) =>
        await db.DialogModes.FirstAsync(mode => mode.Key == DialogModeKeys.CompanyCall);

    [Test]
    public async Task SeedAsync_FreshDatabase_SeedsWarmerPrompts()
    {
        await using var db = CreateInMemory();

        await CompanyCallModeSeeder.SeedAsync(db);

        var mode = await GetSeededModeAsync(db);
        mode.ChatSystemPrompt.Should().Contain("робот-охранник");
        mode.ChatSystemPrompt.Should().Contain("не повод завершать звонок");
        mode.FeedbackSystemPrompt.Should().Contain("доброжелательный тренер");
        mode.FeedbackSystemPrompt.Should().Contain("«пряник»");
    }

    [Test]
    public async Task SeedAsync_ExistingModeWithOldDefaults_UpgradesPromptsInPlace()
    {
        await using var db = CreateInMemory();
        db.DialogModes.Add(new DialogMode
        {
            Id = CompanyCallModeSeeder.CompanyCallModeId,
            BundleId = CompanyCallModeSeeder.CompanyCallBundleId,
            Key = DialogModeKeys.CompanyCall,
            Title = "Холодный звонок",
            Description = "desc",
            ChatSystemPrompt = PreviousChatSystemPrompt,
            FeedbackSystemPrompt = PreviousFeedbackSystemPrompt,
            SortOrder = 1,
            IsActive = true,
            VoiceEnabled = true,
        });
        await db.SaveChangesAsync();

        await CompanyCallModeSeeder.SeedAsync(db);

        var mode = await GetSeededModeAsync(db);
        mode.ChatSystemPrompt.Should().NotBe(PreviousChatSystemPrompt);
        mode.ChatSystemPrompt.Should().Contain("робот-охранник");
        mode.FeedbackSystemPrompt.Should().Contain("«пряник»");
    }

    [Test]
    public async Task SeedAsync_ExistingModeWithAdminCustomizedPrompts_LeavesThemUntouched()
    {
        const string customChat = "Кастомный промпт диалога, отредактированный админом.";
        const string customFeedback = "Кастомный промпт оценки, отредактированный админом.";

        await using var db = CreateInMemory();
        db.DialogModes.Add(new DialogMode
        {
            Id = CompanyCallModeSeeder.CompanyCallModeId,
            BundleId = CompanyCallModeSeeder.CompanyCallBundleId,
            Key = DialogModeKeys.CompanyCall,
            Title = "Холодный звонок",
            Description = "desc",
            ChatSystemPrompt = customChat,
            FeedbackSystemPrompt = customFeedback,
            SortOrder = 1,
            IsActive = true,
            VoiceEnabled = true,
        });
        await db.SaveChangesAsync();

        await CompanyCallModeSeeder.SeedAsync(db);

        var mode = await GetSeededModeAsync(db);
        mode.ChatSystemPrompt.Should().Be(customChat);
        mode.FeedbackSystemPrompt.Should().Be(customFeedback);
    }
}
