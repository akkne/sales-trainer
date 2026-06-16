namespace SalesTrainer.Api.Features.Admin;

/// <summary>
/// Result of a unified content-tree import (skills → topics → lessons → exercises
/// in a single file). Counts are per-level; <see cref="Errors"/> collects
/// per-item problems with a path prefix so partial imports are diagnosable.
/// </summary>
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
