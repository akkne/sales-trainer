namespace Sellevate.Learning.Features.Admin;

public record CreateLessonRequestDto(
    string Title,
    int OrderInTopic
);
