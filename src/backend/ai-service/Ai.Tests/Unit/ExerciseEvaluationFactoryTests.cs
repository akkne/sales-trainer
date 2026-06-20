using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.Ai.Features.Evaluation.Models;
using Sellevate.Ai.Features.Evaluation.Services.Abstract;
using Sellevate.Ai.Features.Evaluation.Services.Implementation;

namespace Sellevate.Ai.Tests.Unit;

[TestFixture]
public class ExerciseEvaluationFactoryTests
{
    private sealed class FakeStrategy(string exerciseType) : IExerciseEvaluationStrategy
    {
        public string SupportedExerciseType => exerciseType;

        public Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
            JsonElement exerciseContent,
            JsonElement userAnswer,
            string? globalSystemPrompt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ExerciseEvaluationResult(true, 100, null, null));
    }

    [Test]
    public void GetStrategyForExerciseType_ReturnsMatchingStrategy()
    {
        var freeText = new FakeStrategy("free_text");
        var rewrite = new FakeStrategy("rewrite");
        var factory = new ExerciseEvaluationFactory([freeText, rewrite]);

        factory.GetStrategyForExerciseType("rewrite").Should().BeSameAs(rewrite);
    }

    [Test]
    public void GetStrategyForExerciseType_ForUnknownType_Throws()
    {
        var factory = new ExerciseEvaluationFactory([new FakeStrategy("free_text")]);

        var act = () => factory.GetStrategyForExerciseType("does_not_exist");

        act.Should().Throw<NotSupportedException>();
    }
}
