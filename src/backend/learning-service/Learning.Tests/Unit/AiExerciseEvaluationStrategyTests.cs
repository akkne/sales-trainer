using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Implementation;
using Sellevate.Learning.Infrastructure.Ai;

namespace Sellevate.Learning.Tests.Unit;

[TestFixture]
public sealed class AiExerciseEvaluationStrategyTests
{
    [Test]
    public async Task EvaluateAnswer_DelegatesToAiServiceWithGlobalPrompt_AndMapsResult()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        databaseContext.ExerciseTypePrompts.Add(new ExerciseTypePrompt
        {
            Id = Guid.NewGuid(),
            ExerciseType = ExerciseTypes.FreeText,
            SystemPrompt = "Grade the free text answer.",
            UpdatedAt = DateTime.UtcNow,
        });
        await databaseContext.SaveChangesAsync();

        var aiEvaluationClient = Substitute.For<IAiEvaluationClient>();
        aiEvaluationClient
            .EvaluateAsync(Arg.Any<AiEvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AiEvaluationResult(true, 90, "Оценка: 9/10", "Great answer"));

        var strategy = new AiExerciseEvaluationStrategy(
            ExerciseTypes.FreeText, aiEvaluationClient, databaseContext);

        var content = JsonDocument.Parse("""{"instruction":"Respond"}""").RootElement;
        var answer = JsonDocument.Parse("""{"text":"My answer"}""").RootElement;

        var result = await strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(90);
        result.AiFeedback.Should().Be("Great answer");

        await aiEvaluationClient.Received(1).EvaluateAsync(
            Arg.Is<AiEvaluationRequest>(request =>
                request.ExerciseType == ExerciseTypes.FreeText
                && request.SystemPrompt == "Grade the free text answer."),
            Arg.Any<CancellationToken>());
    }
}
