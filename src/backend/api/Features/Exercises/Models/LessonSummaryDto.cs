namespace SalesTrainer.Api.Features.Exercises.Models;

public record LessonSummaryDto(
    Guid LessonId,
    string Title,
    string? Description,
    int SortOrder,
    int DifficultyLevel,
    int XpReward,
    int EstimatedMinutes,
    string Status,
    int BestScore
);
