using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Ai.Features.Evaluation.Services.Implementation;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Tests.Unit;

[TestFixture]
public class SpotMistakeEvaluationStrategyTests
{
    private static SpotMistakeEvaluationStrategy CreateStrategy()
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var options = Options.Create(new OpenAiConfiguration { ApiKey = "test-key" });
        return new SpotMistakeEvaluationStrategy(httpClientFactory, options, NullLogger<SpotMistakeEvaluationStrategy>.Instance);
    }

    private const string Dialogue = """
        {
          "dialogue": [
            { "text": "Здравствуйте", "is_mistake": false },
            { "text": "Купите прямо сейчас, это последний шанс!", "is_mistake": true }
          ],
          "explanation": "Давление на клиента отталкивает."
        }
        """;

    [Test]
    public async Task EvaluateAnswer_WrongLine_ScoresZero_AndFails()
    {
        var strategy = CreateStrategy();
        var content = JsonDocument.Parse(Dialogue).RootElement;
        var answer = JsonDocument.Parse("""{ "selectedLineIndex": 0 }""").RootElement;

        var result = await strategy.EvaluateAnswerAsync(content, answer, null);

        result.Score.Should().Be(0);
        result.IsCorrect.Should().BeFalse();
    }

    [Test]
    public async Task EvaluateAnswer_CorrectLine_NoExplanation_ScoresHalf_WithoutCallingAi()
    {
        var strategy = CreateStrategy();
        var content = JsonDocument.Parse(Dialogue).RootElement;
        var answer = JsonDocument.Parse("""{ "selectedLineIndex": 1 }""").RootElement;

        var result = await strategy.EvaluateAnswerAsync(content, answer, null);

        result.Score.Should().Be(50);
        result.IsCorrect.Should().BeFalse();
    }
}
