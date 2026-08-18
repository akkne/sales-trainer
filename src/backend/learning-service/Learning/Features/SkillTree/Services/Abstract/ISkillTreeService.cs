using Sellevate.Learning.Features.SkillTree.Models;

namespace Sellevate.Learning.Features.SkillTree.Services.Abstract;

/// <summary>
/// Reads the skill tree and its stages, and maintains which skills a learner is enrolled in.
///
/// <para>
/// The progress-bearing reads are per-user and tenant-scoped; the counts they return are already
/// resolved for tenant content overrides, so a caller must not add its own de-duplication. A user
/// with no enrollment rows at all counts as enrolled in every skill — see the implementation for why
/// that branch has to stay.
/// </para>
/// </summary>
public interface ISkillTreeService
{
    Task<IReadOnlyList<SkillTreeNodeDto>> GetAllSkillsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SkillTreeNodeDto>> GetAllSkillsWithProgressAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopicDto>> GetTopicsForSkillAsync(
        Guid skillId,
        CancellationToken cancellationToken = default);

    Task<SkillTreeResponseDto> GetSkillTreeForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SkillStageDto>> GetStagesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the user's enrolled skill set with the skills identified by
    /// <paramref name="skillSlugs"/>. The always-on core skill is always kept enrolled.
    /// </summary>
    Task UpdateEnrolledSkillsAsync(
        Guid userId,
        IReadOnlyList<string> skillSlugs,
        CancellationToken cancellationToken = default);
}
