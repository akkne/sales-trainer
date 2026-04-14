using SalesTrainer.Api.Features.Reference.Models;

namespace SalesTrainer.Api.Features.Reference.Services.Abstract;

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
