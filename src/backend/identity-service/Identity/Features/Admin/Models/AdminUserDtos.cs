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

/// <summary>
/// The <c>Manage</c> card on <c>/admin/users</c>. Used to also carry
/// <c>currentStreakDayCount</c>/<c>longestStreakDayCount</c>/<c>totalXpAmount</c>/
/// <c>completedSkillCount</c>/<c>totalSkillCount</c>/<c>averageExerciseScore</c>, sourced from
/// <c>IProfileService.GetProfileStatsForUserAsync</c> — the same identity-service method that
/// hard-codes those fields to zero for <c>GET /profile</c> (see <c>ProfileService</c>). That made
/// this endpoint report "Skills 0/0" for every one of the platform's users even though the system
/// has real skills and completions. b724a2c dropped the fabricated tiles from the admin user
/// modal's UI; this DTO still shipped the same fake zeros over the wire until this fix removed them
/// here too, rather than wiring them to invented data.
/// </summary>
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
    string? Persona);

public sealed record UpdateUserRequestDto(string DisplayName);

public sealed record ChangeUserRoleRequestDto(string Role);
