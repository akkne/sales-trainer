namespace SalesTrainer.Api.Features.Admin;

public record AdminSkillDto(
    Guid Id,
    string IconicName,
    string Title,
    string? Description,
    int OrderInTree,
    string Stage
);
