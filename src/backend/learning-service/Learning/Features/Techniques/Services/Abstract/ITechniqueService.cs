using Sellevate.Learning.Features.Techniques.Models;

namespace Sellevate.Learning.Features.Techniques.Services.Abstract;

/// <summary>
/// Reads the technique library for the current organization and records that a learner has
/// opened a technique.
///
/// <para>
/// Everything returned is already resolved for tenant overrides, so a caller must not re-filter by
/// organization; and every read is scoped to the ambient tenant, so passing a
/// <c>currentUserId</c> from outside that tenant yields empty progress rather than another
/// organization's data.
/// </para>
/// </summary>
public interface ITechniqueService
{
    Task<IReadOnlyList<TechniqueCardDto>> GetTechniqueCardsAsync(
        Guid? currentUserId,
        string? skillIconicName,
        string? searchTerm,
        IReadOnlyCollection<string>? tags,
        CancellationToken cancellationToken = default);

    Task<TechniqueDetailDto?> GetTechniqueBySlugAsync(
        string slug,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<TechniqueMetaDto> GetTechniqueMetaAsync(
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task MarkTechniqueSeenAsync(
        string slug,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
