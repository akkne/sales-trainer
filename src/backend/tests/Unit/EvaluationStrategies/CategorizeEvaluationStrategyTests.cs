using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Exercises.Services.Implementation;

namespace SalesTrainer.Tests.Unit.EvaluationStrategies;

[TestFixture]
public class CategorizeEvaluationStrategyTests
{
    private readonly CategorizeEvaluationStrategy _strategy = new();

    private static JsonElement BuildContent(object[] items, string? explanation = null)
    {
        var obj = new Dictionary<string, object?>
        {
            ["instruction"] = "Разложите по корзинам",
            ["categories"] = new[] { "Открытый", "Закрытый" },
            ["items"] = items,
            ["explanation"] = explanation
        };
        return JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;
    }

    private static JsonElement BuildAnswer(Dictionary<string, string> mapping)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(new { mapping })).RootElement;
    }

    [Test]
    public void SupportedExerciseType_ReturnsCategorize()
    {
        _strategy.SupportedExerciseType.Should().Be("categorize");
    }

    [Test]
    public async Task EvaluateAnswerAsync_AllItemsCorrect_ReturnsScore100()
    {
        var content = BuildContent(new object[]
        {
            new { text = "Какие у вас задачи?", category = "Открытый" },
            new { text = "Вам это интересно?", category = "Закрытый" }
        }, explanation: "Открытые вопросы начинаются с вопросительных слов.");

        var answer = BuildAnswer(new Dictionary<string, string>
        {
            ["0"] = "Открытый",
            ["1"] = "Закрытый"
        });

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(100);
        result.Explanation.Should().Be("Открытые вопросы начинаются с вопросительных слов.");
    }

    [Test]
    public async Task EvaluateAnswerAsync_HalfCorrect_ReturnsPartialCredit()
    {
        var content = BuildContent(new object[]
        {
            new { text = "A", category = "Открытый" },
            new { text = "B", category = "Закрытый" },
            new { text = "C", category = "Открытый" },
            new { text = "D", category = "Закрытый" }
        });

        var answer = BuildAnswer(new Dictionary<string, string>
        {
            ["0"] = "Открытый",  // correct
            ["1"] = "Закрытый",  // correct
            ["2"] = "Закрытый",  // wrong
            ["3"] = "Открытый"   // wrong
        });

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(50);
    }

    [Test]
    public async Task EvaluateAnswerAsync_MissingItems_NotCorrectEvenIfProvidedOnesMatch()
    {
        var content = BuildContent(new object[]
        {
            new { text = "A", category = "Открытый" },
            new { text = "B", category = "Закрытый" }
        });

        // Only one of two items mapped — provided one is right, but answer incomplete
        var answer = BuildAnswer(new Dictionary<string, string> { ["0"] = "Открытый" });

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(50);
    }

    [Test]
    public async Task EvaluateAnswerAsync_AllWrong_ReturnsScore0()
    {
        var content = BuildContent(new object[]
        {
            new { text = "A", category = "Открытый" },
            new { text = "B", category = "Закрытый" }
        });

        var answer = BuildAnswer(new Dictionary<string, string>
        {
            ["0"] = "Закрытый",
            ["1"] = "Открытый"
        });

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0);
    }
}
