using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.ContentTemplating;
using Sellevate.Learning.Features.Content.Services.Abstract;
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
    private readonly IOrganizationProfileProvider _organizationProfileProvider;

    public AiExerciseEvaluationStrategy(
        string exerciseType,
        IAiEvaluationClient aiEvaluationClient,
        LearningDbContext databaseContext,
        IOrganizationProfileProvider organizationProfileProvider)
    {
        _exerciseType = exerciseType;
        _aiEvaluationClient = aiEvaluationClient;
        _databaseContext = databaseContext;
        _organizationProfileProvider = organizationProfileProvider;
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

        // Phase 40.19. The grading criteria are the second half of the banned-claims guarantee. A
        // persona that refuses to voice «мы гарантируем доходность» while this prompt keeps
        // rewarding a rep for saying it teaches exactly the thing compliance forbade — so the same
        // list, in the same words, is appended here and to the persona prompt in ai-service.
        // Appended last, after the exercise-type prompt, so nothing above it can relax the rule.
        var profile = await _organizationProfileProvider.GetCurrentAsync(cancellationToken);
        var systemPrompt = OrganizationPlaceholderRenderer.Render(globalSystemPrompt, profile)
                           + OrganizationProfilePromptBuilder.BuildContextBlock(profile)
                           + OrganizationProfilePromptBuilder.BuildEvaluationBannedClaimsBlock(profile);

        var request = new AiEvaluationRequest(
            _exerciseType,
            string.IsNullOrWhiteSpace(systemPrompt) ? globalSystemPrompt : systemPrompt,
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
