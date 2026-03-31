namespace SalesTrainer.Api.Features.Exercises;

public record LessonSummaryDto(
    Guid LessonId,
    string Title,
    int SortOrder,
    int DifficultyLevel,
    int XpReward,
    string Status,
    int BestScore
);
