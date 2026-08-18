using Sellevate.Learning.Features.ContentGeneration.Models;
using Sellevate.Learning.Features.TeamInsights.Models;

namespace Sellevate.Learning.Features.TeamInsights.Services.Abstract;

/// <summary>
/// Phase 40.31. The loop from «команда проваливает этап» to «вот упражнения на это»
/// (docs/TENANCY/ASSIGNMENTS.md §3.4).
/// </summary>
public interface ITeamSkillGapService
{
    /// <summary>
    /// What the dashboard proposes doing next, over the same window the heat map was drawn over.
    /// </summary>
    Task<TeamSkillGapsDto> GetGapsAsync(int windowDays, CancellationToken cancellationToken = default);

    /// <summary>
    /// The button. Starts a 40.27 content generation run aimed at one failing stage, with material
    /// composed from the measurement and the organization profile.
    ///
    /// <para>
    /// Idempotent while a run for that stage is alive: pressing it twice returns the same run rather
    /// than buying a second lesson about the same weakness.
    /// </para>
    /// </summary>
    Task<ContentGenerationJobDto> StartContentAsync(
        string stageKey,
        Guid? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>«Не сейчас». Records the refusal against the number that provoked it.</summary>
    Task<TeamSkillGapsDto> DismissAsync(
        string stageKey,
        DismissTeamSkillGapRequestDto requestDto,
        Guid? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Takes a refusal back before it expires. Returns false when there was nothing to take back.</summary>
    Task<bool> RestoreAsync(string stageKey, CancellationToken cancellationToken = default);
}
