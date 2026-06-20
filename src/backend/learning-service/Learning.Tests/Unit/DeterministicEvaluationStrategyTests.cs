using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.Learning.Features.Exercises.Services.Implementation;

namespace Sellevate.Learning.Tests.Unit;

[TestFixture]
public sealed class DeterministicEvaluationStrategyTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Test]
    public async Task ChooseOption_CorrectSelection_ScoresFullMarks()
    {
        var strategy = new ChooseOptionEvaluationStrategy();
        var content = Parse("""{"situation":"s","options":[{"text":"a","is_correct":false},{"text":"b","is_correct":true}]}""");
        var answer = Parse("""{"selectedOptionIndex":1}""");

        var result = await strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(100);
    }

    [Test]
    public async Task ChooseOption_WrongSelection_ScoresZero()
    {
        var strategy = new ChooseOptionEvaluationStrategy();
        var content = Parse("""{"options":[{"text":"a","is_correct":true},{"text":"b","is_correct":false}]}""");
        var answer = Parse("""{"selectedOptionIndex":1}""");

        var result = await strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0);
    }

    [Test]
    public async Task MatchPairs_PartialMatch_ScoresProportionally()
    {
        var strategy = new MatchPairsEvaluationStrategy();
        var content = Parse("""{"pairs":[{"left":"a","right":"1"},{"left":"b","right":"2"}]}""");
        var answer = Parse("""{"pairs":[{"left":"a","right":"1"},{"left":"b","right":"9"}]}""");

        var result = await strategy.EvaluateAnswerAsync(content, answer);

        result.Score.Should().Be(50);
        result.IsCorrect.Should().BeFalse();
    }

    [Test]
    public async Task Reorder_ExactOrder_IsCorrect()
    {
        var strategy = new ReorderEvaluationStrategy();
        var content = Parse("""{"items":[{"text":"first","correct_position":1},{"text":"second","correct_position":2}]}""");
        var answer = Parse("""{"order":[0,1]}""");

        var result = await strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(100);
    }

    [Test]
    public async Task Categorize_AllCorrect_IsCorrect()
    {
        var strategy = new CategorizeEvaluationStrategy();
        var content = Parse("""{"items":[{"text":"x","category":"A"},{"text":"y","category":"B"}]}""");
        var answer = Parse("""{"mapping":{"0":"A","1":"B"}}""");

        var result = await strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(100);
    }

    [Test]
    public async Task TheoryCard_AlwaysCompletes()
    {
        var strategy = new TheoryCardEvaluationStrategy();
        var result = await strategy.EvaluateAnswerAsync(Parse("{}"), Parse("{}"));

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(100);
    }
}
