namespace Sellevate.Learning.Features.Exercises.Models;

public record LessonSummaryDto(
    Guid LessonId,
    string Title,
    int OrderInTopic,
    string Status,
    int BestScore,
    string Kind
);
