namespace Sellevate.Learning.Features.Admin;

public record LessonsImportResultDto(int LessonsCreated, int LessonsUpdated, int ExercisesCreated, int ExercisesUpdated, List<string> Errors);
