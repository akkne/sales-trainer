using Sellevate.Learning.Features.SkillTree.Models;

namespace Sellevate.Learning.Features.SkillTree.Services.Abstract;

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
