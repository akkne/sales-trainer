using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Exercises.Services.Implementation;

namespace SalesTrainer.Tests.Unit.EvaluationStrategies;

[TestFixture]
public class ReorderEvaluationStrategyTests
{
    private readonly ReorderEvaluationStrategy _strategy = new();

    private static JsonElement BuildContent(object[] items, string? explanation = null)
    {
        var obj = new Dictionary<string, object?>
        {
            ["instruction"] = "Расставьте реплики по порядку",
            ["items"] = items,
            ["explanation"] = explanation
        };
        return JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;
    }

    private static JsonElement BuildAnswer(int[] order)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(new { order })).RootElement;
    }

    [Test]
    public void SupportedExerciseType_ReturnsReorder()
    {
        _strategy.SupportedExerciseType.Should().Be("reorder");
    }

    [Test]
    public async Task EvaluateAnswerAsync_CorrectOrder_ReturnsScore100()
    {
        // Items listed shuffled: correct sequence is index 1 → 2 → 0
        var content = BuildContent(new object[]
        {
            new { text = "Закрытие", correct_position = 3 },
            new { text = "Приветствие", correct_position = 1 },
            new { text = "Выявление потребности", correct_position = 2 }
        }, explanation: "Классическая структура звонка.");

        var result = await _strategy.EvaluateAnswerAsync(content, BuildAnswer([1, 2, 0]));

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(100);
        result.Explanation.Should().Be("Классическая структура звонка.");
    }

    [Test]
    public async Task EvaluateAnswerAsync_WrongOrder_NoPartialCredit()
    {
        var content = BuildContent(new object[]
        {
            new { text = "A", correct_position = 1 },
            new { text = "B", correct_position = 2 },
            new { text = "C", correct_position = 3 }
        });

        // Two of three positions correct — still 0: exact match only
        var result = await _strategy.EvaluateAnswerAsync(content, BuildAnswer([0, 2, 1]));

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0);
    }

    [Test]
    public async Task EvaluateAnswerAsync_AnswerLengthMismatch_ReturnsIncorrect()
    {
        var content = BuildContent(new object[]
        {
            new { text = "A", correct_position = 1 },
            new { text = "B", correct_position = 2 }
        });

        var result = await _strategy.EvaluateAnswerAsync(content, BuildAnswer([0]));

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0);
    }

    [Test]
    public async Task EvaluateAnswerAsync_NoExplanationInContent_ReturnsNullExplanation()
    {
        var content = BuildContent(new object[]
        {
            new { text = "A", correct_position = 1 }
        });

        var result = await _strategy.EvaluateAnswerAsync(content, BuildAnswer([0]));

        result.IsCorrect.Should().BeTrue();
        result.Explanation.Should().BeNull();
        result.AiFeedback.Should().BeNull();
    }
}
