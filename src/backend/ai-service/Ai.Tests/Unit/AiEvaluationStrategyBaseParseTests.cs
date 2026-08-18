using FluentAssertions;
using NUnit.Framework;
using Sellevate.Ai.Features.Evaluation.Services.Implementation;
using Sellevate.Ai.Features.Quotas.Services.Abstract;

namespace Sellevate.Ai.Tests.Unit;

/// <summary>
/// Tests for AiEvaluationStrategyBase.ParseAiResponse — malformed grader JSON paths (AI5).
/// </summary>
[TestFixture]
public class AiEvaluationStrategyBaseParseTests
{
    /// <summary>
    /// <c>ParseAiResponse</c> is <c>protected static</c> on an <c>internal</c> class, and
    /// <c>InternalsVisibleTo</c> is set, so a thin subclass is enough to reach it without making the
    /// method itself more visible than it should be.
    /// </summary>
    private sealed class Exposed : AiEvaluationStrategyBase
    {
        public Exposed() : base(null!, null!, null!, null!) { }

        public static global::Sellevate.Ai.Features.Evaluation.Models.ExerciseEvaluationResult Parse(string json)
            => ParseAiResponse(json);
    }

    [Test]
    public void ParseAiResponse_ValidJson_ReturnsExpected()
    {
        var result = Exposed.Parse("""{"passed": true, "rating": 8, "feedback": "Good"}""");

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(80);
        result.AiFeedback.Should().Be("Good");
    }

    /// <summary>A model that returns the rating as a quoted string still scores.</summary>
    [Test]
    public void ParseAiResponse_RatingAsString_ParsedGracefully()
    {
        var result = Exposed.Parse("""{"passed": false, "rating": "7", "feedback": "ok"}""");

        result.Score.Should().Be(70);
        result.IsCorrect.Should().BeFalse();
    }

    [Test]
    public void ParseAiResponse_RatingOutOfRange_High_ClampedTo10()
    {
        var result = Exposed.Parse("""{"passed": true, "rating": 99, "feedback": "overrated"}""");

        result.Score.Should().Be(100);
    }

    [Test]
    public void ParseAiResponse_RatingOutOfRange_Low_ClampedTo1()
    {
        var result = Exposed.Parse("""{"passed": false, "rating": -5, "feedback": "bad"}""");

        result.Score.Should().Be(10);
    }

    /// <summary>
    /// <c>passed</c> arrives as the string <c>"true"</c>, which parses as the boolean. The result is
    /// correct on the strength of that alone: a rating of 6 would not have reached the
    /// rating-at-least-8 threshold on its own.
    /// </summary>
    [Test]
    public void ParseAiResponse_PassedAsStringTrue_TreatedAsTrue()
    {
        var result = Exposed.Parse("""{"passed": "true", "rating": 6, "feedback": "ok"}""");

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(60);
    }

    /// <summary>
    /// Every field has the wrong type — <c>passed</c> is a number, <c>rating</c> is an object. Nothing
    /// throws: a non-zero number reads as true and the rating falls back to its default of 5.
    /// </summary>
    [Test]
    public void ParseAiResponse_WrongJsonTypes_PassedIsNumber_DegradeGracefully()
    {
        var result = Exposed.Parse("""{"passed": 1, "rating": {"value": 5}, "feedback": null}""");

        result.Should().NotBeNull();
        result.Score.Should().Be(50);
    }

    /// <summary>Not JSON at all: the answer degrades to a failed result rather than an exception.</summary>
    [Test]
    public void ParseAiResponse_CompletelyUnparseable_DegradeToFailedResult()
    {
        var result = Exposed.Parse("Sorry, I cannot provide a score right now.");

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0);
        result.AiFeedback.Should().BeNull();
    }

    /// <summary>No fields at all: not passed, and the rating falls back to its default of 5.</summary>
    [Test]
    public void ParseAiResponse_EmptyJson_DegradeGracefully()
    {
        var result = Exposed.Parse("{}");

        result.Should().NotBeNull();
        result.Score.Should().Be(50);
        result.IsCorrect.Should().BeFalse();
    }

    [Test]
    public void ParseAiResponse_RatingExactly10_NotClamped()
    {
        var result = Exposed.Parse("""{"passed": true, "rating": 10, "feedback": "perfect"}""");

        result.Score.Should().Be(100);
        result.IsCorrect.Should().BeTrue();
    }

    [Test]
    public void ParseAiResponse_RatingExactly1_NotClamped()
    {
        var result = Exposed.Parse("""{"passed": false, "rating": 1, "feedback": "terrible"}""");

        result.Score.Should().Be(10);
        result.IsCorrect.Should().BeFalse();
    }
}
