namespace Sellevate.Learning.Features.Admin;

public record CreateTopicRequestDto(
    string IconicName,
    string Title,
    int OrderInSkill
);
