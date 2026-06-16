namespace SalesTrainer.Api.Features.Exercises.Models;

public record LessonSummaryDto(
    Guid LessonId,
    string Title,
    int OrderInTopic,
    string Status,
    int BestScore,
    // "theory" when every exercise in the lesson is a theory_card, otherwise "practice".
    string Kind
);
