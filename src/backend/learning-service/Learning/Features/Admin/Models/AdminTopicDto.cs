namespace Sellevate.Learning.Features.Admin;

public record AdminTopicDto(
    Guid Id,
    Guid SkillId,
    string IconicName,
    string Title,
    int OrderInSkill
);
