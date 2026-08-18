using Sellevate.Learning.Features.TeamInsights.Models;

namespace Sellevate.Learning.Features.TeamInsights.Services.Abstract;

/// <summary>
/// Phase 40.25. The team-level half of the РОП's dashboard (docs/TENANCY/ASSIGNMENTS.md §4).
/// Read-only, bounded to the caller's organization by <c>ITenantContext</c>, and never takes an
/// organization as an argument (docs/TENANCY/TENANCY.md §1.3).
/// </summary>
public interface ITeamSkillMapService
{
    /// <summary>
    /// The heat map over the last <paramref name="windowDays"/> days.
    /// </summary>
    /// <param name="windowDays">
    /// How far back to count, clamped to a sane range by the implementation. A window rather than
    /// all history because the number is sold as team readiness now, and practice from a year ago
    /// is not evidence about now.
    /// </param>
    Task<TeamSkillMapDto> GetSkillMapAsync(
        int windowDays,
        CancellationToken cancellationToken = default);
}
