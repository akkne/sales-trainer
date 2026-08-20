using Sellevate.Learning.Features.Reference.Models;

namespace Sellevate.Learning.Features.Reference.Services.Abstract;

public interface IReferenceService
{
    /// <summary>
    /// Resolves <paramref name="skillIdentifier"/> as either the skill's GUID or its slug
    /// (<c>IconicName</c>) - the same dual acceptance as <c>GetLessonsForSkillAsync</c> on the
    /// sibling <c>/skills/{id}/lessons</c> endpoint. Returns an empty list for an unknown
    /// identifier, matching that endpoint's "empty list, not 404" contract for the happy path.
    /// </summary>
    Task<IReadOnlyList<ReferenceMaterialDto>> GetReferenceMaterialsForSkillAsync(
        string skillIdentifier,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReferenceMaterialDto>> GetAllReferenceMaterialsAsync(
        string? category,
        string? search,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAllCategoriesAsync(
        CancellationToken cancellationToken = default);
}
