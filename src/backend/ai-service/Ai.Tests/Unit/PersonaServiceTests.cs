using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Ai.Features.Companies.Models;
using Sellevate.Ai.Features.Companies.Services.Implementation;
using Sellevate.Ai.Features.Dialog.Services.Abstract;

namespace Sellevate.Ai.Tests.Unit;

[TestFixture]
public class PersonaServiceTests
{
    private IOpenAiChatService _openAiChatService = null!;
    private PersonaService _personaService = null!;

    [SetUp]
    public void SetUp()
    {
        _openAiChatService = Substitute.For<IOpenAiChatService>();
        _openAiChatService.IsConfigured.Returns(true);
        _personaService = new PersonaService(_openAiChatService, NullLogger<PersonaService>.Instance);
    }

    private static GeneratePersonaRequestDto BuildRequest(PersonaDifficulty difficulty = PersonaDifficulty.Medium) =>
        new("Поставщик офисных принадлежностей", "Иван Петров", "Закупщик", difficulty);

    [Test]
    public async Task GeneratePersonaAsync_ReturnsParsedFields_ForWellFormedJson()
    {
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"name\": \"Мария Соколова\", \"position\": \"Руководитель закупок\", \"personality\": \"Прагматична и вежлива, любит цифры.\"}");

        var result = await _personaService.GeneratePersonaAsync(BuildRequest());

        result.Name.Should().Be("Мария Соколова");
        result.Position.Should().Be("Руководитель закупок");
        result.Personality.Should().Be("Прагматична и вежлива, любит цифры.");
    }

    [Test]
    public async Task GeneratePersonaAsync_ParsesJson_WhenWrappedInMarkdownCodeFence()
    {
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("```json\n{\"name\": \"Мария\", \"position\": \"Закупщик\", \"personality\": \"Скептична.\"}\n```");

        var result = await _personaService.GeneratePersonaAsync(BuildRequest());

        result.Name.Should().Be("Мария");
        result.Position.Should().Be("Закупщик");
        result.Personality.Should().Be("Скептична.");
    }

    [Test]
    public async Task GeneratePersonaAsync_ThrowsInvalidOperationException_WhenAiReturnsNonJson()
    {
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Извините, я не могу это сделать.");

        var act = () => _personaService.GeneratePersonaAsync(BuildRequest());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task GeneratePersonaAsync_ThrowsInvalidOperationException_WhenAiReturnsNonObjectJson()
    {
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("[1, 2, 3]");

        var act = () => _personaService.GeneratePersonaAsync(BuildRequest());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [TestCase("{\"position\": \"Закупщик\", \"personality\": \"Скептична.\"}")]
    [TestCase("{\"name\": \"Мария\", \"personality\": \"Скептична.\"}")]
    [TestCase("{\"name\": \"Мария\", \"position\": \"Закупщик\"}")]
    [TestCase("{\"name\": \"\", \"position\": \"Закупщик\", \"personality\": \"Скептична.\"}")]
    public async Task GeneratePersonaAsync_ThrowsInvalidOperationException_WhenFieldsMissingOrEmpty(string aiJson)
    {
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(aiJson);

        var act = () => _personaService.GeneratePersonaAsync(BuildRequest());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task GeneratePersonaAsync_ThrowsWhenNotConfigured()
    {
        _openAiChatService.IsConfigured.Returns(false);

        var act = () => _personaService.GeneratePersonaAsync(BuildRequest());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [TestCase(PersonaDifficulty.Easy)]
    [TestCase(PersonaDifficulty.Medium)]
    [TestCase(PersonaDifficulty.Hard)]
    public async Task GeneratePersonaAsync_SendsDifficultyAwareSystemPrompt(PersonaDifficulty difficulty)
    {
        _openAiChatService
            .GenerateTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"name\": \"Мария\", \"position\": \"Закупщик\", \"personality\": \"Скептична.\"}");

        await _personaService.GeneratePersonaAsync(BuildRequest(difficulty));

        await _openAiChatService.Received(1).GenerateTextAsync(
            Arg.Is<string>(prompt => prompt.Contains("сложности")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
