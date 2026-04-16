using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Exercises.Services.Implementation;

namespace SalesTrainer.Tests.Unit.EvaluationStrategies;

[TestFixture]
public class MatchPairsEvaluationStrategyTests
{
    private readonly MatchPairsEvaluationStrategy _strategy = new();

    private static JsonElement BuildContent(object[] pairs, string? explanation = null)
    {
        var obj = new Dictionary<string, object?>
        {
            ["instruction"] = "Соедините пары",
            ["pairs"] = pairs,
            ["explanation"] = explanation
        };
        return JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;
    }

    private static JsonElement BuildAnswer(object[] pairs)
    {
        return JsonDocument.Parse(
            JsonSerializer.Serialize(new { pairs })).RootElement;
    }

    [Test]
    public void SupportedExerciseType_ReturnsMatchPairs()
    {
        _strategy.SupportedExerciseType.Should().Be("match_pairs");
    }

    [Test]
    public async Task EvaluateAnswerAsync_AllPairsCorrect_ReturnsScore100()
    {
        var pairs = new object[]
        {
            new { left = "Потребность", right = "Проблема клиента" },
            new { left = "Возражение", right = "Сомнение клиента" },
            new { left = "Закрытие", right = "Завершение сделки" }
        };
        var content = BuildContent(pairs, explanation: "Основные термины продаж.");

        var userPairs = new object[]
        {
            new { left = "Потребность", right = "Проблема клиента" },
            new { left = "Возражение", right = "Сомнение клиента" },
            new { left = "Закрытие", right = "Завершение сделки" }
        };
        var answer = BuildAnswer(userPairs);

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(100);
        result.Explanation.Should().Be("Основные термины продаж.");
    }

    [Test]
    public async Task EvaluateAnswerAsync_TwoOfThreeCorrect_ReturnsScore66()
    {
        var pairs = new object[]
        {
            new { left = "A", right = "1" },
            new { left = "B", right = "2" },
            new { left = "C", right = "3" }
        };
        var content = BuildContent(pairs);

        var userPairs = new object[]
        {
            new { left = "A", right = "1" },
            new { left = "B", right = "2" },
            new { left = "C", right = "2" } // Wrong!
        };
        var answer = BuildAnswer(userPairs);

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().BeInRange(60, 70); // ~66%
    }

    [Test]
    public async Task EvaluateAnswerAsync_AllPairsWrong_ReturnsScore0()
    {
        var pairs = new object[]
        {
            new { left = "A", right = "1" },
            new { left = "B", right = "2" }
        };
        var content = BuildContent(pairs);

        var userPairs = new object[]
        {
            new { left = "A", right = "2" },
            new { left = "B", right = "1" }
        };
        var answer = BuildAnswer(userPairs);

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0);
    }

    [Test]
    public async Task EvaluateAnswerAsync_EmptyUserPairs_ReturnsScore0()
    {
        var pairs = new object[]
        {
            new { left = "A", right = "1" },
            new { left = "B", right = "2" }
        };
        var content = BuildContent(pairs);
        var answer = BuildAnswer(Array.Empty<object>());

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0);
    }

    [Test]
    public async Task EvaluateAnswerAsync_PartialPairs_CalculatesCorrectPercentage()
    {
        var pairs = new object[]
        {
            new { left = "1", right = "A" },
            new { left = "2", right = "B" },
            new { left = "3", right = "C" },
            new { left = "4", right = "D" }
        };
        var content = BuildContent(pairs);

        // Only 1 out of 4 correct
        var userPairs = new object[]
        {
            new { left = "1", right = "A" }, // Correct
            new { left = "2", right = "C" }, // Wrong
            new { left = "3", right = "D" }, // Wrong
            new { left = "4", right = "B" }  // Wrong
        };
        var answer = BuildAnswer(userPairs);

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(25); // 1/4 = 25%
    }
}
