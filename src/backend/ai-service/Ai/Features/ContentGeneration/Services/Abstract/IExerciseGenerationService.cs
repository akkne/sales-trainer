using Sellevate.Ai.Features.ContentGeneration.Models;

namespace Sellevate.Ai.Features.ContentGeneration.Services.Abstract;

/// <summary>
/// Phase 40.27. The second half: turn a <b>confirmed</b> structure into exercises. It never sees the
/// raw material, which is what makes the human's edit at the checkpoint binding rather than advisory.
/// </summary>
public interface IExerciseGenerationService
{
    Task<GeneratedLessonDto> GenerateAsync(
        GenerateExercisesRequestDto request,
        CancellationToken cancellationToken = default);
}
