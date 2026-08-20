namespace Sellevate.Ai.Infrastructure.Learning;

/// <summary>
/// C-3 audit fix. Asks learning-service for the slug and title behind each skill id dialog content
/// is filed under, since ai-service owns the bundle but not the skill it names
/// (docs/AUDIT_CONTRACTS.md, finding C-3).
/// </summary>
public interface ISkillLookupClient
{
    /// <summary>
    /// Every known skill, keyed by id. Never throws: a failure to reach learning-service degrades to
    /// an empty map, and callers render a bundle with an unlabelled skill rather than lose the whole
    /// dialog list over a service that has nothing to do with dialog itself being unreachable.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SkillSummary>> GetSkillSummariesAsync(
        CancellationToken cancellationToken = default);
}
