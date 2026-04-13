namespace SalesTrainer.Api.Features.Exercises.Models;

public record ExerciseChatRequestDto(string Message);

public record ExerciseChatResponseDto(
    string Response,
    bool IsComplete,
    int TurnNumber,
    int MaxTurns);
