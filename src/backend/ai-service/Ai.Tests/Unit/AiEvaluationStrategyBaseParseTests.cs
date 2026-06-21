using FluentAssertions;
using NUnit.Framework;
using Sellevate.Ai.Features.Evaluation.Services.Implementation;

namespace Sellevate.Ai.Tests.Unit;

/// <summary>
/// Tests for AiEvaluationStrategyBase.ParseAiResponse — malformed grader JSON paths (AI5).
/// </summary>
[TestFixture]
public class AiEvaluationStrategyBaseParseTests
{
    // ParseAiResponse is protected static, but InternalsVisibleTo is set and the class is internal.
    // We use a thin test subclass to expose it.
    private sealed class Exposed : AiEvaluationStrategyBase
    {
        public Exposed() : base(null!, null!, null!) { }

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

    [Test]
    public void ParseAiResponse_RatingAsString_ParsedGracefully()
    {
        // AI returns rating as a quoted string instead of a number
        var result = Exposed.Parse("""{"passed": false, "rating": "7", "feedback": "ok"}""");

        result.Score.Should().Be(70);
        result.IsCorrect.Should().BeFalse(); // rating 7 < 8 and passed=false
    }

    [Test]
    public void ParseAiResponse_RatingOutOfRange_High_ClampedTo10()
    {
        var result = Exposed.Parse("""{"passed": true, "rating": 99, "feedback": "overrated"}""");

        result.Score.Should().Be(100); // clamped to 10, score = 10*10
    }

    [Test]
    public void ParseAiResponse_RatingOutOfRange_Low_ClampedTo1()
    {
        var result = Exposed.Parse("""{"passed": false, "rating": -5, "feedback": "bad"}""");

        result.Score.Should().Be(10); // clamped to 1, score = 1*10
    }

    [Test]
    public void ParseAiResponse_PassedAsStringTrue_TreatedAsTrue()
    {
        // passed is the string "true" — GetBooleanSafe should parse it as true
        var result = Exposed.Parse("""{"passed": "true", "rating": 6, "feedback": "ok"}""");

        // IsCorrect = passed(true) || rating>=8(false) = true
        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(60);
    }

    [Test]
    public void ParseAiResponse_WrongJsonTypes_PassedIsNumber_DegradeGracefully()
    {
        // passed is a number (not bool/string), rating is an object — completely wrong types
        var result = Exposed.Parse("""{"passed": 1, "rating": {"value": 5}, "feedback": null}""");

        // Should not throw; rating defaults to 5, passed=true (nonzero number)
        result.Should().NotBeNull();
        result.Score.Should().Be(50); // default rating 5
    }

    [Test]
    public void ParseAiResponse_CompletelyUnparseable_DegradeToFailedResult()
    {
        // Not JSON at all
        var result = Exposed.Parse("Sorry, I cannot provide a score right now.");

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(0);
        result.AiFeedback.Should().BeNull();
    }

    [Test]
    public void ParseAiResponse_EmptyJson_DegradeGracefully()
    {
        var result = Exposed.Parse("{}");

        // No fields present: passed=false, rating defaults to 5
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
