using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.ContentTemplating;
using Sellevate.Learning.Features.Content.Services.Abstract;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

/// <summary>
/// Grades the exercise types no deterministic rule can judge — the open-ended ones — by asking
/// ai-service.
///
/// <para>
/// <b>One instance per exercise type, constructed rather than injected.</b> The supported type is a
/// constructor argument because every AI-graded type shares this implementation and differs only in
/// which system prompt it loads; <see cref="ExerciseEvaluationFactory"/> builds one per entry in
/// <c>ExerciseTypes.AiPowered</c>. That is why this class is not DI-registered like its deterministic
/// siblings.
/// </para>
///
/// <para>
/// <b>The banned-claims block is appended last, and that position is the guarantee.</b> A persona that
/// refuses to voice «мы гарантируем доходность» while the grading prompt still rewards a rep for saying
/// it teaches exactly the thing compliance forbade — so the same list, in the same words, goes to the
/// grader here and to the persona prompt in ai-service. Appended after the exercise-type prompt so
/// nothing above it can relax the rule; a block inserted earlier could be overridden by author text.
/// </para>
///
/// <para>
/// A type with no stored system prompt still grades: the assembled prompt falls back to the raw
/// (possibly empty) one, and ai-service applies its own defaults.
/// </para>
/// </summary>
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
