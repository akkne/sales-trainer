namespace Sellevate.Learning.Features.Admin;

public record UpdateTopicRequestDto(
    string? IconicName,
    string? Title,
    int? OrderInSkill
);
