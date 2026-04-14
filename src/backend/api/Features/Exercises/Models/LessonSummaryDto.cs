namespace SalesTrainer.Api.Features.Exercises.Models;

public record LessonSummaryDto(
    Guid LessonId,
    string Title,
    int OrderInTopic,
    string Status,
    int BestScore
);
