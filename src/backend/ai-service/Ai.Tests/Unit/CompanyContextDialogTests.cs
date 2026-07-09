using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Ai.Eventing;
using Sellevate.Ai.Features.Dialog.Constants;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Seeders;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Features.Dialog.Services.Implementation;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.Ai.Infrastructure.Mongo;
using Microsoft.Extensions.Configuration;

namespace Sellevate.Ai.Tests.Unit;

[TestFixture]
public class CompanyContextDialogTests
{
    private static AiDbContext BuildInMemoryContext()
    {
        var databaseOptions = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase("company-context-test-" + Guid.NewGuid())
            .Options;
        return new AiDbContext(databaseOptions);
    }

    private static MongoDbContext BuildFakeMongoContext()
    {
        var mongoClient = Substitute.For<IMongoClient>();
        var mongoDatabase = Substitute.For<IMongoDatabase>();
        mongoClient.GetDatabase(Arg.Any<string>(), Arg.Any<MongoDatabaseSettings>()).Returns(mongoDatabase);
        var configuration = new ConfigurationBuilder().Build();
        return new MongoDbContext(mongoClient, configuration);
    }

    private static DialogSession BuildSessionWithContext(
        Guid modeId,
        CompanyCallContext? companyCallContext)
    {
        return new DialogSession
        {
            Id = "507f1f77bcf86cd799439011",
            UserId = Guid.NewGuid(),
            BundleId = Guid.NewGuid(),
            ModeId = modeId,
            Status = DialogSessionStatus.Active,
            Messages = [],
            CompanyCallContext = companyCallContext
        };
    }

    [Test]
    public async Task Seeder_CreatesCompanyCallBundle_AndMode_OnFirstRun()
    {
        await using var databaseContext = BuildInMemoryContext();

        await CompanyCallModeSeeder.SeedAsync(databaseContext);

        var mode = await databaseContext.DialogModes
            .FirstOrDefaultAsync(dialogMode => dialogMode.Key == DialogModeKeys.CompanyCall);

        mode.Should().NotBeNull();
        mode!.VoiceEnabled.Should().BeTrue();
        mode.IsActive.Should().BeTrue();

        var bundle = await databaseContext.DialogBundles
            .FirstOrDefaultAsync(dialogBundle => dialogBundle.Id == mode.BundleId);

        bundle.Should().NotBeNull();
        bundle!.IsHidden.Should().BeTrue();
        bundle.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task Seeder_IsIdempotent_DoesNotDuplicateOnSecondRun()
    {
        await using var databaseContext = BuildInMemoryContext();

        await CompanyCallModeSeeder.SeedAsync(databaseContext);
        await CompanyCallModeSeeder.SeedAsync(databaseContext);

        var modeCount = await databaseContext.DialogModes
            .CountAsync(dialogMode => dialogMode.Key == DialogModeKeys.CompanyCall);

        modeCount.Should().Be(1);
    }

    [Test]
    public async Task GetActiveBundles_ExcludesHiddenBundles()
    {
        await using var databaseContext = BuildInMemoryContext();
        var mongoContext = BuildFakeMongoContext();

        var visibleSkillId = Guid.NewGuid();
        databaseContext.DialogBundles.Add(new DialogBundle
        {
            Id = Guid.NewGuid(),
            SkillId = visibleSkillId,
            Title = "Visible Bundle",
            Description = "A visible bundle",
            IconEmoji = "📞",
            SortOrder = 1,
            IsActive = true,
            IsHidden = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        databaseContext.DialogBundles.Add(new DialogBundle
        {
            Id = Guid.NewGuid(),
            SkillId = Guid.NewGuid(),
            Title = "Hidden Bundle",
            Description = "A hidden bundle",
            IconEmoji = "🏢",
            SortOrder = 0,
            IsActive = true,
            IsHidden = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await databaseContext.SaveChangesAsync();

        var openAiChatService = Substitute.For<IOpenAiChatService>();
        openAiChatService.IsConfigured.Returns(true);

        var dialogService = new DialogService(
            databaseContext,
            mongoContext,
            openAiChatService,
            Substitute.For<IDialogScoringWeightsProvider>(),
            Substitute.For<IDialogEventPublisher>(),
            NullLogger<DialogService>.Instance);

        var bundles = await dialogService.GetActiveBundlesAsync();

        bundles.Should().HaveCount(1);
        bundles[0].Title.Should().Be("Visible Bundle");
    }

    [Test]
    public void ChatSystemPrompt_WithCompanyContext_AppendsCompanyBlock()
    {
        var basePrompt = "Ты — менеджер по продажам.";
        var companyCallContext = new CompanyCallContext
        {
            CompanyName = "ООО Рога и Копыта",
            CompanyDescription = "Поставщик офисных принадлежностей",
            CallGoal = "Записать встречу"
        };
        var session = BuildSessionWithContext(Guid.NewGuid(), companyCallContext);

        var composedPrompt = BuildTestChatSystemPrompt(basePrompt, session.CompanyCallContext);

        composedPrompt.Should().Contain(basePrompt);
        composedPrompt.Should().Contain("Компания: ООО Рога и Копыта");
        composedPrompt.Should().Contain("Описание: Поставщик офисных принадлежностей");
        composedPrompt.Should().Contain("Цель звонка пользователя: Записать встречу");
    }

    [Test]
    public void FeedbackSystemPrompt_WithCompanyContext_AppendsCompanyBlock()
    {
        var basePrompt = "Оцени разговор пользователя.";
        var companyCallContext = new CompanyCallContext
        {
            CompanyName = "Технопром",
            CompanyDescription = "ИТ-интегратор",
            CallGoal = null
        };
        var session = BuildSessionWithContext(Guid.NewGuid(), companyCallContext);

        var composedPrompt = BuildTestChatSystemPrompt(basePrompt, session.CompanyCallContext);

        composedPrompt.Should().Contain(basePrompt);
        composedPrompt.Should().Contain("Компания: Технопром");
        composedPrompt.Should().Contain("Описание: ИТ-интегратор");
        composedPrompt.Should().NotContain("Цель звонка пользователя");
    }

    [Test]
    public void ChatSystemPrompt_WithoutCompanyContext_ReturnsBasePromptUnchanged()
    {
        var basePrompt = "Ты — менеджер по продажам.";
        var session = BuildSessionWithContext(Guid.NewGuid(), null);

        var composedPrompt = BuildTestChatSystemPrompt(basePrompt, session.CompanyCallContext);

        composedPrompt.Should().Be(basePrompt);
    }

    [Test]
    public void ChatSystemPrompt_WithEmptyCallGoal_OmitsCallGoalLine()
    {
        var basePrompt = "Ты — менеджер.";
        var companyCallContext = new CompanyCallContext
        {
            CompanyName = "Компания А",
            CompanyDescription = "Описание",
            CallGoal = "   "
        };

        var composedPrompt = BuildTestChatSystemPrompt(basePrompt, companyCallContext);

        composedPrompt.Should().NotContain("Цель звонка пользователя");
    }

    [Test]
    public void DialogSession_PersistsCompanyCallContext_WhenCreated()
    {
        var companyCallContext = new CompanyCallContext
        {
            CompanyName = "Лидер продаж",
            CompanyDescription = "Дистрибьютор электроники",
            CallGoal = "Выйти на ЛПР"
        };

        var session = new DialogSession
        {
            UserId = Guid.NewGuid(),
            BundleId = Guid.NewGuid(),
            ModeId = Guid.NewGuid(),
            Status = DialogSessionStatus.Active,
            Messages = [],
            CompanyCallContext = companyCallContext
        };

        session.CompanyCallContext.Should().NotBeNull();
        session.CompanyCallContext!.CompanyName.Should().Be("Лидер продаж");
        session.CompanyCallContext.CompanyDescription.Should().Be("Дистрибьютор электроники");
        session.CompanyCallContext.CallGoal.Should().Be("Выйти на ЛПР");
    }

    [Test]
    public void DialogSession_WithNullCompanyCallContext_IsValid()
    {
        var session = new DialogSession
        {
            UserId = Guid.NewGuid(),
            BundleId = Guid.NewGuid(),
            ModeId = Guid.NewGuid(),
            Status = DialogSessionStatus.Active,
            Messages = [],
            CompanyCallContext = null
        };

        session.CompanyCallContext.Should().BeNull();
    }

    private static string BuildTestChatSystemPrompt(string basePrompt, CompanyCallContext? companyCallContext)
    {
        if (companyCallContext == null)
        {
            return basePrompt;
        }

        var lines = new System.Text.StringBuilder();
        lines.Append(basePrompt);
        lines.AppendLine();
        lines.AppendLine();
        lines.AppendLine("---");
        lines.AppendLine($"Компания: {companyCallContext.CompanyName}");
        lines.AppendLine($"Описание: {companyCallContext.CompanyDescription}");

        if (!string.IsNullOrWhiteSpace(companyCallContext.CallGoal))
        {
            lines.AppendLine($"Цель звонка пользователя: {companyCallContext.CallGoal}");
        }

        return lines.ToString();
    }
}
