using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Exercises.Services.Implementation;

namespace SalesTrainer.Tests.Unit.EvaluationStrategies;

[TestFixture]
public class ChooseOptionEvaluationStrategyTests
{
    private readonly ChooseOptionEvaluationStrategy _strategy = new();

    private static JsonElement BuildContent(object[] options, string? explanation = null)
    {
        var obj = new Dictionary<string, object?>
        {
            ["situation"] = "Test situation",
            ["options"] = options,
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
        var options = new object[]
        {
            new { text = "Wrong", is_correct = false },
            new { text = "Wrong", is_correct = false },
            new { text = "Correct", is_correct = true },
            new { text = "Wrong", is_correct = false }
        };
        var content = BuildContent(options, explanation: "Because it works.");
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
        var options = new object[]
        {
            new { text = "Wrong", is_correct = false },
            new { text = "Correct", is_correct = true },
            new { text = "Wrong", is_correct = false }
        };
        var content = BuildContent(options);
        var answer = BuildAnswer(selectedOptionIndex: 0);

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0);
    }

    [Test]
    public async Task EvaluateAnswerAsync_NoExplanationInContent_ExplanationIsNull()
    {
        var options = new object[]
        {
            new { text = "Correct", is_correct = true },
            new { text = "Wrong", is_correct = false }
        };
        var content = BuildContent(options);
        var answer = BuildAnswer(selectedOptionIndex: 0);

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.Explanation.Should().BeNull();
    }
}
