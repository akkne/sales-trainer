using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Exercises;

namespace SalesTrainer.Tests.Unit.EvaluationStrategies;

[TestFixture]
public class MultipleChoiceEvaluationStrategyTests
{
    private readonly MultipleChoiceEvaluationStrategy _strategy = new();

    private static JsonElement BuildContent(int correctOptionIndex, string? explanation = null)
    {
        var obj = new Dictionary<string, object?>
        {
            ["correctOptionIndex"] = correctOptionIndex,
            ["explanation"] = explanation
        };
        return JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;
    }

    private static JsonElement BuildAnswer(int selectedOptionIndex)
    {
        return JsonDocument.Parse(
            JsonSerializer.Serialize(new { selectedOptionIndex })).RootElement;
    }

    [Test]
    public async Task EvaluateAnswerAsync_CorrectAnswer_ReturnsIsCorrectTrueScore100()
    {
        var content = BuildContent(correctOptionIndex: 2, explanation: "Because it works.");
        var answer = BuildAnswer(selectedOptionIndex: 2);

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(100);
        result.Explanation.Should().Be("Because it works.");
        result.AiFeedback.Should().BeNull();
    }

    [Test]
    public async Task EvaluateAnswerAsync_WrongAnswer_ReturnsIsCorrectFalseScore0()
    {
        var content = BuildContent(correctOptionIndex: 1);
        var answer = BuildAnswer(selectedOptionIndex: 0);

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0);
    }

    [Test]
    public async Task EvaluateAnswerAsync_NoExplanationInContent_ExplanationIsNull()
    {
        var content = JsonDocument.Parse(
            JsonSerializer.Serialize(new { correctOptionIndex = 0 })).RootElement;
        var answer = BuildAnswer(selectedOptionIndex: 0);

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.Explanation.Should().BeNull();
    }
}
