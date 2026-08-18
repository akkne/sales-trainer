using Sellevate.Ai.Features.ContentGeneration.Models;

namespace Sellevate.Ai.Features.ContentGeneration.Services.Abstract;

/// <summary>
/// Phase 40.27. The first half of the admin content pipeline: read the РОП's material and say what
/// is in it, without generating anything.
/// </summary>
public interface IMaterialStructuringService
{
    Task<ExtractedContentStructureDto> ExtractAsync(
        StructureMaterialRequestDto request,
        CancellationToken cancellationToken = default);
}
