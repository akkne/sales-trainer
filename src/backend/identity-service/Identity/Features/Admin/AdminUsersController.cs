using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Features.Admin.Constants;
using Sellevate.Identity.Features.Admin.Models;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Avatars;
using Sellevate.Identity.Features.Avatars.Services.Abstract;
using Sellevate.Identity.Features.Profile.Services.Abstract;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Admin;

/// <summary>
/// Platform-wide user administration: this controller lists and manages ALL users, not the roster of
/// one organization, so it stays Sellevate-staff-only.
///
/// <para>
/// From the 2026-08-16 role-split audit: reading the roster is ordinary platform administration and
/// is open to <c>Admin</c> as well as <c>SuperAdmin</c>; every mutation here adds, removes or
/// re-roles a user, and that is the single privilege reserved for <c>SuperAdmin</c>
/// (docs/DECISIONS.md).
/// </para>
/// </summary>
[ApiController]
[Route("admin/users")]
[Authorize(Policy = AuthorizationPolicies.RequirePlatformAdministrator)]
public sealed class AdminUsersController(
    IdentityDbContext database,
    IAvatarService avatarService,
    IProfileService profileService,
    ILogger<AdminUsersController> logger) : ControllerBase
{
    private const int DisplayNameMinimumLength = 2;
    private const int DisplayNameMaximumLength = 50;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await database.Users
            .OrderBy(user => user.CreatedAt)
            .Select(user => new AdminUserDto(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Role.ToString(),
                user.CreatedAt,
                user.IsEmailVerified,
                user.GoogleId != null ? AuthProviderLabels.Google : AuthProviderLabels.Password,
                user.AvatarType == AvatarKind.Uploaded,
                AvatarUrls.For(user.Id)))
            .ToListAsync(cancellationToken);

        logger.LogInformation("Admin user list fetched by {ActorId}, count={Count}",
            User.FindFirstValue(ClaimTypes.NameIdentifier), users.Count);

        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminUserDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var stats = await profileService.GetProfileStatsForUserAsync(id, cancellationToken);

        return Ok(new AdminUserDetailDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role.ToString(),
            user.CreatedAt,
            user.IsEmailVerified,
            user.GoogleId != null ? AuthProviderLabels.Google : AuthProviderLabels.Password,
            user.AvatarType == AvatarKind.Uploaded,
            AvatarUrls.For(user.Id),
            stats.CurrentStreakDayCount,
            stats.LongestStreakDayCount,
            stats.TotalXpAmount,
            stats.CompletedSkillCount,
            stats.TotalSkillCount,
            stats.AverageExerciseScore,
            stats.Persona));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.RequireSuperAdministrator)]
    public async Task<ActionResult<AdminUserDto>> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequestDto request,
        CancellationToken cancellationToken)
    {
        var displayName = (request.DisplayName ?? "").Trim();
        if (displayName.Length is < DisplayNameMinimumLength or > DisplayNameMaximumLength)
        {
            return BadRequest(new
            {
                message = $"Display name must be between {DisplayNameMinimumLength} and {DisplayNameMaximumLength} characters."
            });
        }

        var user = await database.Users.FindAsync([id], cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var previousName = user.DisplayName;
        user.DisplayName = displayName;
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User display name changed TargetUserId={TargetUserId} \"{OldName}\" -> \"{NewName}\" by ActorId={ActorId}",
            user.Id, previousName, displayName, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(ToAdminUserDto(user));
    }

    [HttpDelete("{id:guid}/avatar")]
    [Authorize(Policy = AuthorizationPolicies.RequireSuperAdministrator)]
    public async Task<IActionResult> DeleteAvatar(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await avatarService.ResetToDefaultAsync(id, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        logger.LogInformation("User avatar reset by admin TargetUserId={TargetUserId} ActorId={ActorId}",
            id, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }

    /// <summary>
    /// <c>Enum.TryParse</c> also accepts bare numbers and returns undefined values for them ("99"
    /// would become <c>(UserRole)99</c>), so the parse is paired with an <c>IsDefined</c> check.
    /// Demoting the last remaining superadministrator is refused: it would leave the platform with
    /// nobody able to promote anyone back.
    /// </summary>
    [HttpPut("{id:guid}/role")]
    [Authorize(Policy = AuthorizationPolicies.RequireSuperAdministrator)]
    public async Task<ActionResult<AdminUserDto>> ChangeRole(
        Guid id,
        [FromBody] ChangeUserRoleRequestDto request,
        CancellationToken cancellationToken)
    {
        var user = await database.Users.FindAsync([id], cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var newRole)
            || !Enum.IsDefined(newRole))
        {
            return BadRequest(new { message = $"Unknown role: {request.Role}" });
        }

        var previousRole = user.Role;

        if (previousRole == UserRole.SuperAdmin && newRole != UserRole.SuperAdmin)
        {
            var superAdminCount = await database.Users
                .CountAsync(candidate => candidate.Role == UserRole.SuperAdmin, cancellationToken);
            if (superAdminCount <= 1)
            {
                return Conflict(new { message = "Cannot demote the last SuperAdmin." });
            }
        }

        user.Role = newRole;
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User role changed TargetUserId={TargetUserId} Email={Email} {OldRole} -> {NewRole} by ActorId={ActorId}",
            user.Id, user.Email, previousRole, newRole, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(ToAdminUserDto(user));
    }

    private static AdminUserDto ToAdminUserDto(User user)
        => new(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role.ToString(),
            user.CreatedAt,
            user.IsEmailVerified,
            user.GoogleId != null ? AuthProviderLabels.Google : AuthProviderLabels.Password,
            user.AvatarType == AvatarKind.Uploaded,
            AvatarUrls.For(user.Id));
}
