using SalesTrainer.Api.Features.Techniques.Models;

namespace SalesTrainer.Api.Features.Techniques.Services.Abstract;

public interface ITechniqueService
{
    Task<IReadOnlyList<TechniqueCardDto>> GetTechniqueCardsAsync(
        Guid? currentUserId,
        string? skillIconicName,
        string? searchTerm,
        string? tag,
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
