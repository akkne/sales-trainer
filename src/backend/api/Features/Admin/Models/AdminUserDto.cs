namespace SalesTrainer.Api.Features.Admin;

public record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    DateTime CreatedAt,
    bool IsEmailVerified,
    string AuthProvider,
    bool HasCustomAvatar,
    string AvatarUrl
);

public record AdminUserDetailDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    DateTime CreatedAt,
    bool IsEmailVerified,
    string AuthProvider,
    bool HasCustomAvatar,
    string AvatarUrl,
    int CurrentStreakDayCount,
    int LongestStreakDayCount,
    int TotalXpAmount,
    int CompletedSkillCount,
    int TotalSkillCount,
    double AverageExerciseScore,
    string? Persona
);
