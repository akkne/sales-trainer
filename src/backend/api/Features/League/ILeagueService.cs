namespace SalesTrainer.Api.Features.League;

public interface ILeagueService
{
    Task<CurrentLeagueResponseDto> GetCurrentLeagueForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task CloseCurrentLeagueAndCreateNextAsync(
        CancellationToken cancellationToken = default);
}
