namespace Sellevate.Learning.Features.Admin;

public record BundleImportResultDto(
    int SkillsCreated,
    int SkillsUpdated,
    int TopicsCreated,
    int TopicsUpdated,
    int LessonsCreated,
    int LessonsUpdated,
    int ExercisesCreated,
    int ExercisesUpdated,
    List<string> Errors);
