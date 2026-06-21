namespace Sellevate.Identity.Features.Admin.Models;

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    DateTime CreatedAt,
    bool IsEmailVerified,
    string AuthProvider,
    bool HasCustomAvatar,
    string AvatarUrl);

public sealed record AdminUserDetailDto(
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
    string? Persona);

public sealed record UpdateUserRequestDto(string DisplayName);

public sealed record ChangeUserRoleRequestDto(string Role);
