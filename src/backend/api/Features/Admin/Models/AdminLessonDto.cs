namespace SalesTrainer.Api.Features.Admin;

public record AdminLessonDto(
    Guid Id,
    Guid TopicId,
    string Title,
    int OrderInTopic
);
