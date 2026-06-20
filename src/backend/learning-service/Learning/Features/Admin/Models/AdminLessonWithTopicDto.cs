namespace Sellevate.Learning.Features.Admin;

public record AdminLessonWithTopicDto(
    Guid Id,
    Guid TopicId,
    string TopicIconicName,
    string TopicTitle,
    string Title,
    int OrderInTopic
);
