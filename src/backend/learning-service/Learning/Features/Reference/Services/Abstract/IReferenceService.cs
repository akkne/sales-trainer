using Sellevate.Learning.Features.Reference.Models;

namespace Sellevate.Learning.Features.Reference.Services.Abstract;

public interface IReferenceService
{
    Task<IReadOnlyList<ReferenceMaterialDto>> GetReferenceMaterialsForSkillAsync(
        Guid skillId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReferenceMaterialDto>> GetAllReferenceMaterialsAsync(
        string? category,
        string? search,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAllCategoriesAsync(
        CancellationToken cancellationToken = default);
}
