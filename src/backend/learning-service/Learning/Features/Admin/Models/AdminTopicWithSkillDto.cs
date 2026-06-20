namespace Sellevate.Learning.Features.Admin;

public record AdminTopicWithSkillDto(
    Guid Id,
    Guid SkillId,
    string SkillIconicName,
    string SkillTitle,
    string IconicName,
    string Title,
    int OrderInSkill
);
