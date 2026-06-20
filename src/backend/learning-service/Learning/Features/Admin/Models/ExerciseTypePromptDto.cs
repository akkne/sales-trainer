namespace Sellevate.Learning.Features.Admin;

public record ExerciseTypePromptDto(
    Guid Id,
    string ExerciseType,
    string SystemPrompt,
    DateTime UpdatedAt
);
