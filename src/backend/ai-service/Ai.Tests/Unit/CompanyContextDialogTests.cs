using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Ai.Eventing;
using Sellevate.Ai.Features.Dialog.Constants;
using Sellevate.Ai.Features.Dialog.Helpers;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Seeders;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Features.Dialog.Services.Implementation;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.Ai.Infrastructure.Mongo;

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
        mode.Id.Should().Be(CompanyCallModeSeeder.CompanyCallModeId);

        var bundle = await databaseContext.DialogBundles
            .FirstOrDefaultAsync(dialogBundle => dialogBundle.Id == mode.BundleId);

        bundle.Should().NotBeNull();
        bundle!.IsHidden.Should().BeTrue();
        bundle.IsActive.Should().BeTrue();
        bundle.Id.Should().Be(CompanyCallModeSeeder.CompanyCallBundleId);
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

        databaseContext.DialogBundles.Add(new DialogBundle
        {
            Id = Guid.NewGuid(),
            SkillId = Guid.NewGuid(),
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

        var composedPrompt = CompanyContextPromptBuilder.BuildChatSystemPrompt(basePrompt, companyCallContext);

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

        var composedPrompt = CompanyContextPromptBuilder.BuildFeedbackSystemPrompt(basePrompt, companyCallContext);

        composedPrompt.Should().Contain(basePrompt);
        composedPrompt.Should().Contain("Компания: Технопром");
        composedPrompt.Should().Contain("Описание: ИТ-интегратор");
        composedPrompt.Should().NotContain("Цель звонка пользователя");
    }

    [Test]
    public void ChatSystemPrompt_WithoutCompanyContext_ReturnsBasePromptUnchanged()
    {
        var basePrompt = "Ты — менеджер по продажам.";

        var composedPrompt = CompanyContextPromptBuilder.BuildChatSystemPrompt(basePrompt, null);

        composedPrompt.Should().Be(basePrompt);
    }

    [Test]
    public void FeedbackSystemPrompt_WithoutCompanyContext_ReturnsBasePromptUnchanged()
    {
        var basePrompt = "Оцени разговор.";

        var composedPrompt = CompanyContextPromptBuilder.BuildFeedbackSystemPrompt(basePrompt, null);

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

        var composedPrompt = CompanyContextPromptBuilder.BuildChatSystemPrompt(basePrompt, companyCallContext);

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

    [Test]
    public async Task StartSession_WithCompanyContext_OnNonCompanyCallMode_Throws()
    {
        await using var databaseContext = BuildInMemoryContext();
        var mongoContext = BuildFakeMongoContext();

        var regularModeId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        databaseContext.DialogBundles.Add(new DialogBundle
        {
            Id = bundleId,
            SkillId = Guid.NewGuid(),
            Title = "Regular Bundle",
            Description = "Description",
            IconEmoji = "📞",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        databaseContext.DialogModes.Add(new DialogMode
        {
            Id = regularModeId,
            BundleId = bundleId,
            Key = "regular-mode",
            Title = "Regular Mode",
            Description = "Description",
            ChatSystemPrompt = "Prompt",
            FeedbackSystemPrompt = "Feedback",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await databaseContext.SaveChangesAsync();

        var dialogService = new DialogService(
            databaseContext,
            mongoContext,
            Substitute.For<IOpenAiChatService>(),
            Substitute.For<IDialogScoringWeightsProvider>(),
            Substitute.For<IDialogEventPublisher>(),
            NullLogger<DialogService>.Instance);

        var companyCallContext = new CompanyCallContext
        {
            CompanyName = "Test Company",
            CompanyDescription = "Test Description"
        };

        var act = () => dialogService.StartSessionAsync(Guid.NewGuid(), bundleId, regularModeId, companyCallContext);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*company-call*");
    }

    [Test]
    public async Task GetCompanyCallMode_ReturnsNull_WhenModeNotSeeded()
    {
        await using var databaseContext = BuildInMemoryContext();
        var mongoContext = BuildFakeMongoContext();

        var dialogService = new DialogService(
            databaseContext,
            mongoContext,
            Substitute.For<IOpenAiChatService>(),
            Substitute.For<IDialogScoringWeightsProvider>(),
            Substitute.For<IDialogEventPublisher>(),
            NullLogger<DialogService>.Instance);

        var mode = await dialogService.GetCompanyCallModeAsync();

        mode.Should().BeNull();
    }

    [Test]
    public async Task GetCompanyCallMode_ReturnsSeededMode_AfterSeeding()
    {
        await using var databaseContext = BuildInMemoryContext();
        var mongoContext = BuildFakeMongoContext();

        await CompanyCallModeSeeder.SeedAsync(databaseContext);

        var dialogService = new DialogService(
            databaseContext,
            mongoContext,
            Substitute.For<IOpenAiChatService>(),
            Substitute.For<IDialogScoringWeightsProvider>(),
            Substitute.For<IDialogEventPublisher>(),
            NullLogger<DialogService>.Instance);

        var mode = await dialogService.GetCompanyCallModeAsync();

        mode.Should().NotBeNull();
        mode!.Key.Should().Be(DialogModeKeys.CompanyCall);
        mode.Id.Should().Be(CompanyCallModeSeeder.CompanyCallModeId);
    }
}
