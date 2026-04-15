using SalesTrainer.Api.Features.SkillTree.Models;

namespace SalesTrainer.Api.Features.SkillTree.Services.Abstract;

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
}
