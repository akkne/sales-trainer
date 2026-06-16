using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Exercises.Services.Implementation;

namespace SalesTrainer.Tests.Unit.EvaluationStrategies;

[TestFixture]
public class TheoryCardEvaluationStrategyTests
{
    private readonly TheoryCardEvaluationStrategy _strategy = new();

    private static JsonElement Json(object obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;

    [Test]
    public void SupportedExerciseType_IsTheoryCard()
    {
        _strategy.SupportedExerciseType.Should().Be("theory_card");
    }

    [Test]
    public async Task EvaluateAnswerAsync_AlwaysCorrect_NeverInvokesAi()
    {
        // Theory cards carry no answer; reaching the end completes the lesson.
        var content = Json(new { layout = "text", title = "Intro", body = "Some theory." });
        var answer = Json(new { });

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(100);
        result.Explanation.Should().BeNull();
        result.AiFeedback.Should().BeNull();
    }
}
