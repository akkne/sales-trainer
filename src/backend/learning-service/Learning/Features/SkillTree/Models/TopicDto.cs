namespace Sellevate.Learning.Features.SkillTree.Models;

public record TopicDto(
    Guid TopicId,
    Guid SkillId,
    string Title,
    int OrderInSkill
);
