namespace Sellevate.Learning.Features.Exercises.Models;

public record ExerciseChatResponseDto(
    string Response,
    bool IsComplete,
    bool IsFinished,
    int TurnNumber,
    int MaxTurns);
