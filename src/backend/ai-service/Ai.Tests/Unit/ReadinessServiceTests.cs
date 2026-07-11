using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Ai.Features.Companies.Models;
using Sellevate.Ai.Features.Companies.Services.Implementation;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Services.Abstract;

namespace Sellevate.Ai.Tests.Unit;

[TestFixture]
public class ReadinessServiceTests
{
    private IDialogService _dialogService = null!;
    private IOpenAiChatService _openAiChatService = null!;
    private ReadinessService _readinessService = null!;

    [SetUp]
    public void SetUp()
    {
        _dialogService = Substitute.For<IDialogService>();
        _openAiChatService = Substitute.For<IOpenAiChatService>();
        _openAiChatService.IsConfigured.Returns(true);
        _readinessService = new ReadinessService(_dialogService, _openAiChatService, NullLogger<ReadinessService>.Instance);
    }

    private static DialogSession SessionWithFeedback(string sessionId, string? summary) => new()
    {
        Id = sessionId,
        UserId = Guid.NewGuid(),
        BundleId = Guid.NewGuid(),
        ModeId = Guid.NewGuid(),
        Feedback = summary is null ? null : new DialogFeedback { Summary = summary, Content = "content", GeneratedAt = DateTime.UtcNow }
    };

    private static readonly Guid TestUserId = Guid.NewGuid();

    private static GenerateReadinessRequestDto BuildRequest(params string[] sessionIds) =>
        new(TestUserId, "Записать встречу", sessionIds.ToList());

    [Test]
    public async Task GenerateReadinessAsync_ReturnsParsedFields_ForWellFormedJson()
    {
        _dialogService.GetSessionForUserAsync("s1", TestUserId, Arg.Any<CancellationToken>())
            .Returns(SessionWithFeedback("s1", "Хорошо держал паузу, но забыл про цену."));
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"score\": 72, \"strengths\": [\"Уверенный тон\"], \"gaps\": [\"Работа с ценой\"], \"recommendation\": \"Потренируйте возражения по цене.\"}");

        var result = await _readinessService.GenerateReadinessAsync(BuildRequest("s1"));

        result.Should().NotBeNull();
        result!.Score.Should().Be(72);
        result.Strengths.Should().ContainSingle().Which.Should().Be("Уверенный тон");
        result.Gaps.Should().ContainSingle().Which.Should().Be("Работа с ценой");
        result.Recommendation.Should().Be("Потренируйте возражения по цене.");
    }

    [Test]
    public async Task GenerateReadinessAsync_ParsesJson_WhenWrappedInMarkdownCodeFence()
    {
        _dialogService.GetSessionForUserAsync("s1", TestUserId, Arg.Any<CancellationToken>())
            .Returns(SessionWithFeedback("s1", "Резюме звонка."));
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("```json\n{\"score\": 50, \"strengths\": [], \"gaps\": [], \"recommendation\": \"Продолжайте практиковаться.\"}\n```");

        var result = await _readinessService.GenerateReadinessAsync(BuildRequest("s1"));

        result.Should().NotBeNull();
        result!.Score.Should().Be(50);
        result.Recommendation.Should().Be("Продолжайте практиковаться.");
    }

    [Test]
    public async Task GenerateReadinessAsync_ThrowsInvalidOperationException_WhenAiReturnsNonJson()
    {
        _dialogService.GetSessionForUserAsync("s1", TestUserId, Arg.Any<CancellationToken>())
            .Returns(SessionWithFeedback("s1", "Резюме."));
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Извините, не могу это сделать.");

        var act = () => _readinessService.GenerateReadinessAsync(BuildRequest("s1"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task GenerateReadinessAsync_ThrowsInvalidOperationException_WhenScoreMissing()
    {
        _dialogService.GetSessionForUserAsync("s1", TestUserId, Arg.Any<CancellationToken>())
            .Returns(SessionWithFeedback("s1", "Резюме."));
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"strengths\": [], \"gaps\": [], \"recommendation\": \"Ок.\"}");

        var act = () => _readinessService.GenerateReadinessAsync(BuildRequest("s1"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task GenerateReadinessAsync_ThrowsInvalidOperationException_WhenRecommendationMissing()
    {
        _dialogService.GetSessionForUserAsync("s1", TestUserId, Arg.Any<CancellationToken>())
            .Returns(SessionWithFeedback("s1", "Резюме."));
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"score\": 40, \"strengths\": [], \"gaps\": []}");

        var act = () => _readinessService.GenerateReadinessAsync(BuildRequest("s1"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [TestCase(150, 100)]
    [TestCase(-10, 0)]
    public async Task GenerateReadinessAsync_ClampsScore_ToZeroToHundredRange(int rawScore, int expectedScore)
    {
        _dialogService.GetSessionForUserAsync("s1", TestUserId, Arg.Any<CancellationToken>())
            .Returns(SessionWithFeedback("s1", "Резюме."));
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns($"{{\"score\": {rawScore}, \"strengths\": [], \"gaps\": [], \"recommendation\": \"Ок.\"}}");

        var result = await _readinessService.GenerateReadinessAsync(BuildRequest("s1"));

        result!.Score.Should().Be(expectedScore);
    }

    [Test]
    public async Task GenerateReadinessAsync_ReturnsNull_WhenNoSessionsHaveFeedback()
    {
        _dialogService.GetSessionForUserAsync("s1", TestUserId, Arg.Any<CancellationToken>())
            .Returns(SessionWithFeedback("s1", null));
        _dialogService.GetSessionForUserAsync("s2", TestUserId, Arg.Any<CancellationToken>())
            .Returns((DialogSession?)null);

        var result = await _readinessService.GenerateReadinessAsync(BuildRequest("s1", "s2"));

        result.Should().BeNull();
        await _openAiChatService.DidNotReceive()
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GenerateReadinessAsync_FiltersOutSessionsWithoutFeedback_AndUsesRemaining()
    {
        _dialogService.GetSessionForUserAsync("s1", TestUserId, Arg.Any<CancellationToken>())
            .Returns(SessionWithFeedback("s1", null));
        _dialogService.GetSessionForUserAsync("s2", TestUserId, Arg.Any<CancellationToken>())
            .Returns(SessionWithFeedback("s2", "Хорошее резюме."));
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"score\": 60, \"strengths\": [], \"gaps\": [], \"recommendation\": \"Ок.\"}");

        var result = await _readinessService.GenerateReadinessAsync(BuildRequest("s1", "s2"));

        result.Should().NotBeNull();
        await _openAiChatService.Received(1).GenerateTextAsync(
            Arg.Any<string>(),
            Arg.Is<string>(prompt => prompt.Contains("Хорошее резюме.") && !prompt.Contains("null")),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GenerateReadinessAsync_ThrowsWhenNotConfigured()
    {
        _openAiChatService.IsConfigured.Returns(false);

        var act = () => _readinessService.GenerateReadinessAsync(BuildRequest("s1"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
