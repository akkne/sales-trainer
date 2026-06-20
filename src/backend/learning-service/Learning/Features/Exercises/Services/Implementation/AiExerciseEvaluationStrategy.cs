using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

internal sealed class AiExerciseEvaluationStrategy : IExerciseEvaluationStrategy
{
    private readonly string _exerciseType;
    private readonly IAiEvaluationClient _aiEvaluationClient;
    private readonly LearningDbContext _databaseContext;

    public AiExerciseEvaluationStrategy(
        string exerciseType,
        IAiEvaluationClient aiEvaluationClient,
        LearningDbContext databaseContext)
    {
        _exerciseType = exerciseType;
        _aiEvaluationClient = aiEvaluationClient;
        _databaseContext = databaseContext;
    }

    public string SupportedExerciseType => _exerciseType;

    public async Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        var globalSystemPrompt = await _databaseContext.ExerciseTypePrompts
            .Where(prompt => prompt.ExerciseType == _exerciseType)
            .Select(prompt => prompt.SystemPrompt)
            .FirstOrDefaultAsync(cancellationToken);

        var request = new AiEvaluationRequest(
            _exerciseType,
            globalSystemPrompt,
            exerciseContent,
            userAnswer);

        var result = await _aiEvaluationClient.EvaluateAsync(request, cancellationToken);

        return new ExerciseEvaluationResult(
            result.IsCorrect,
            result.Score,
            result.Explanation,
            result.AiFeedback);
    }
}
