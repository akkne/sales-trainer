namespace SalesTrainer.Api.Features.Admin;

public record CreateSkillRequestDto(
    string IconicName,
    string Title,
    string? Description,
    int OrderInTree,
    string? Stage
);
