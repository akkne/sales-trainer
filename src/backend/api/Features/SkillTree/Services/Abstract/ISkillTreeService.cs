using SalesTrainer.Api.Features.SkillTree.Models;

namespace SalesTrainer.Api.Features.SkillTree.Services.Abstract;

public interface ISkillTreeService
{
    Task UpdateEnrolledSkillsAsync(
        Guid userId,
        List<string> skillSlugs,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SkillTreeNodeDto>> GetAllSkillsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SkillTreeResponseDto> GetSkillTreeForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
