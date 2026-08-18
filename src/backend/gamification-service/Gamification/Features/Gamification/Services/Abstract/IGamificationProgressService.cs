using Sellevate.Gamification.Features.Gamification.Models;

namespace Sellevate.Gamification.Features.Gamification.Services.Abstract;

/// <summary>
/// Builds the progress read model: streak counts, experience-point totals for the day, the week and
/// all time, and the goals they are measured against. Read-only.
/// </summary>
public interface IGamificationProgressService
{
    Task<GamificationProgressDto> GetProgressForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
