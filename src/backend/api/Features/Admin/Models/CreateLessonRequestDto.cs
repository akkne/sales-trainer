namespace SalesTrainer.Api.Features.Admin;

public record CreateLessonRequestDto(
    string Title,
    int OrderInTopic
);
