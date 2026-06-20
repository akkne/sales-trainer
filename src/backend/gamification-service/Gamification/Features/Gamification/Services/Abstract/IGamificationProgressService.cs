using Sellevate.Gamification.Features.Gamification.Models;

namespace Sellevate.Gamification.Features.Gamification.Services.Abstract;

public interface IGamificationProgressService
{
    Task<GamificationProgressDto> GetProgressForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
