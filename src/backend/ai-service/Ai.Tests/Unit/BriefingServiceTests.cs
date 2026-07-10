using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Ai.Features.Companies.Models;
using Sellevate.Ai.Features.Companies.Services.Implementation;
using Sellevate.Ai.Features.Dialog.Services.Abstract;

namespace Sellevate.Ai.Tests.Unit;

[TestFixture]
public class BriefingServiceTests
{
    private IOpenAiChatService _openAiChatService = null!;
    private BriefingService _briefingService = null!;

    [SetUp]
    public void SetUp()
    {
        _openAiChatService = Substitute.For<IOpenAiChatService>();
        _openAiChatService.IsConfigured.Returns(true);
        _briefingService = new BriefingService(_openAiChatService);
    }

    [Test]
    public async Task GenerateBriefingAsync_ReturnsContentFromChatService()
    {
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("## Кто они\n- Тестовая компания");

        var request = new GenerateBriefingRequestDto("Описание компании", null, [], []);

        var result = await _briefingService.GenerateBriefingAsync(request);

        result.Should().Be("## Кто они\n- Тестовая компания");
    }

    [Test]
    public async Task GenerateBriefingAsync_ThrowsWhenNotConfigured()
    {
        _openAiChatService.IsConfigured.Returns(false);
        var request = new GenerateBriefingRequestDto("Описание", null, [], []);

        var act = () => _briefingService.GenerateBriefingAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task GenerateBriefingAsync_SystemPromptIncludesCompanyDescriptionAndGoal()
    {
        string? capturedSystemPrompt = null;
        _openAiChatService
            .GenerateTextAsync(Arg.Do<string>(prompt => capturedSystemPrompt = prompt), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("content");

        var request = new GenerateBriefingRequestDto("Продаёт виджеты", "Договориться о демо", [], []);

        await _briefingService.GenerateBriefingAsync(request);

        capturedSystemPrompt.Should().Contain("Продаёт виджеты");
        capturedSystemPrompt.Should().Contain("Договориться о демо");
    }

    [Test]
    public async Task GenerateBriefingAsync_SystemPromptIncludesRecentCallsAndFeedbackSummaries()
    {
        string? capturedSystemPrompt = null;
        _openAiChatService
            .GenerateTextAsync(Arg.Do<string>(prompt => capturedSystemPrompt = prompt), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("content");

        var request = new GenerateBriefingRequestDto(
            "Описание",
            null,
            [new BriefingCallLogDto("Иван Петров", "Обсудили условия", "Взял паузу подумать", new DateTime(2026, 7, 1))],
            ["Уверенно закрыл возражение по цене"]);

        await _briefingService.GenerateBriefingAsync(request);

        capturedSystemPrompt.Should().Contain("Иван Петров");
        capturedSystemPrompt.Should().Contain("Обсудили условия");
        capturedSystemPrompt.Should().Contain("Уверенно закрыл возражение по цене");
    }

    [Test]
    public async Task GenerateBriefingAsync_HandlesEmptyRecentCallsAndFeedback_WithoutThrowing()
    {
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("content");

        var request = new GenerateBriefingRequestDto("Описание", null, [], []);

        var act = () => _briefingService.GenerateBriefingAsync(request);

        await act.Should().NotThrowAsync();
    }
}
