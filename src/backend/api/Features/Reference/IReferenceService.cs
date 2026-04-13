namespace SalesTrainer.Api.Features.Reference;

public interface IReferenceService
{
    Task<IReadOnlyList<ReferenceMaterialDto>> GetReferenceMaterialsForSkillAsync(
        string skillSlug,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReferenceMaterialDto>> GetAllReferenceMaterialsAsync(
        string? category,
        string? search,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAllCategoriesAsync(
        CancellationToken cancellationToken = default);
}
