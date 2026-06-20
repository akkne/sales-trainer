namespace Sellevate.Identity.Features.Profile.Models;

public record UpdatePersonaRequestDto(string Persona);

public record UserProfileStatsDto(
    string DisplayName,
    string Email,
    int CurrentStreakDayCount,
    int LongestStreakDayCount,
    int TotalXpAmount,
    int CompletedSkillCount,
    int TotalSkillCount,
    double AverageExerciseScore,
    string? Persona,
    string AvatarUrl
);
