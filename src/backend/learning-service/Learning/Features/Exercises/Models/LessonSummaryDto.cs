namespace Sellevate.Learning.Features.Exercises.Models;

public record LessonSummaryDto(
    Guid LessonId,
    string Title,
    int OrderInTopic,
    int TopicOrder,
    string Status,
    int BestScore,
    string Kind
);
