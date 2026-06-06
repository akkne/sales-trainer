namespace SalesTrainer.Api.Features.Admin;

public record CreateTopicRequestDto(
    string IconicName,
    string Title,
    int OrderInSkill
);
