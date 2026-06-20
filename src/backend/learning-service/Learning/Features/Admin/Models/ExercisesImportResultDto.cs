namespace Sellevate.Learning.Features.Admin;

public record ExercisesImportResultDto(
    int ExercisesCreated,
    int ExercisesUpdated,
    List<string> Errors
);
