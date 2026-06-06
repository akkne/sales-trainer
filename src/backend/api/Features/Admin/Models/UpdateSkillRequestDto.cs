namespace SalesTrainer.Api.Features.Admin;

public record UpdateSkillRequestDto(
    string? IconicName,
    string? Title,
    string? Description,
    int? OrderInTree,
    string? Stage
);
