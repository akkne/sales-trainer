using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Ai.Features.Companies.Services.Implementation;
using Sellevate.Ai.Features.Dialog.Services.Abstract;

namespace Sellevate.Ai.Tests.Unit;

[TestFixture]
public class ParseLogServiceTests
{
    private IOpenAiChatService _openAiChatService = null!;
    private ParseLogService _parseLogService = null!;

    [SetUp]
    public void SetUp()
    {
        _openAiChatService = Substitute.For<IOpenAiChatService>();
        _openAiChatService.IsConfigured.Returns(true);
        _parseLogService = new ParseLogService(_openAiChatService, NullLogger<ParseLogService>.Instance);
    }

    [Test]
    public async Task ParseLogAsync_ReturnsParsedFields_ForWellFormedJson()
    {
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"contactName\": \"Иван Петров\", \"subject\": \"Обсудили условия\", \"outcome\": \"Взял паузу подумать\", \"occurredAt\": \"2026-07-01\"}");

        var result = await _parseLogService.ParseLogAsync("какие-то сырые заметки");

        result.ContactName.Should().Be("Иван Петров");
        result.Subject.Should().Be("Обсудили условия");
        result.Outcome.Should().Be("Взял паузу подумать");
        result.OccurredAt.Should().Be(new DateTime(2026, 7, 1));
    }

    [Test]
    public async Task ParseLogAsync_ReturnsNullOccurredAt_WhenDateMissing()
    {
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"contactName\": null, \"subject\": \"Звонок\", \"outcome\": \"Договорились созвониться позже\", \"occurredAt\": null}");

        var result = await _parseLogService.ParseLogAsync("текст без даты");

        result.ContactName.Should().BeNull();
        result.OccurredAt.Should().BeNull();
    }

    [Test]
    public async Task ParseLogAsync_ReturnsNullOccurredAt_WhenDateIsUnparseable()
    {
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"subject\": \"Звонок\", \"outcome\": \"Итог\", \"occurredAt\": \"когда-то давно\"}");

        var result = await _parseLogService.ParseLogAsync("текст");

        result.OccurredAt.Should().BeNull();
    }

    [Test]
    public async Task ParseLogAsync_ThrowsInvalidOperationException_WhenAiReturnsNonJson()
    {
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Извините, я не могу это сделать.");

        var act = () => _parseLogService.ParseLogAsync("текст");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task ParseLogAsync_ThrowsInvalidOperationException_WhenAiReturnsNonObjectJson()
    {
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("[1, 2, 3]");

        var act = () => _parseLogService.ParseLogAsync("текст");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task ParseLogAsync_ThrowsWhenNotConfigured()
    {
        _openAiChatService.IsConfigured.Returns(false);

        var act = () => _parseLogService.ParseLogAsync("текст");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task ParseLogAsync_DefaultsSubjectAndOutcomeToEmpty_WhenMissingFromJson()
    {
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"contactName\": \"Иван\"}");

        var result = await _parseLogService.ParseLogAsync("текст");

        result.Subject.Should().BeEmpty();
        result.Outcome.Should().BeEmpty();
    }
}
