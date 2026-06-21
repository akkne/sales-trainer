using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Features.Admin.Models;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Avatars;
using Sellevate.Identity.Features.Avatars.Services.Abstract;
using Sellevate.Identity.Features.Profile.Services.Abstract;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Admin;

[ApiController]
[Route("admin/users")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminUsersController(
    IdentityDbContext database,
    IAvatarService avatarService,
    IProfileService profileService,
    ILogger<AdminUsersController> logger) : ControllerBase
{
    private const int DisplayNameMinLength = 2;
    private const int DisplayNameMaxLength = 50;

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
                user.GoogleId != null ? "Google" : "Password",
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
            user.GoogleId != null ? "Google" : "Password",
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
    public async Task<ActionResult<AdminUserDto>> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequestDto request,
        CancellationToken cancellationToken)
    {
        var displayName = (request.DisplayName ?? "").Trim();
        if (displayName.Length is < DisplayNameMinLength or > DisplayNameMaxLength)
        {
            return BadRequest(new
            {
                message = $"Display name must be between {DisplayNameMinLength} and {DisplayNameMaxLength} characters."
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

        return Ok(new AdminUserDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role.ToString(),
            user.CreatedAt,
            user.IsEmailVerified,
            user.GoogleId != null ? "Google" : "Password",
            user.AvatarType == AvatarKind.Uploaded,
            AvatarUrls.For(user.Id)));
    }

    [HttpDelete("{id:guid}/avatar")]
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

    [HttpPut("{id:guid}/role")]
    [Authorize(Policy = "RequireSuperAdmin")]
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

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var newRole))
        {
            return BadRequest(new { message = $"Unknown role: {request.Role}" });
        }

        var previousRole = user.Role;
        user.Role = newRole;
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User role changed TargetUserId={TargetUserId} Email={Email} {OldRole} -> {NewRole} by ActorId={ActorId}",
            user.Id, user.Email, previousRole, newRole, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminUserDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role.ToString(),
            user.CreatedAt,
            user.IsEmailVerified,
            user.GoogleId != null ? "Google" : "Password",
            user.AvatarType == AvatarKind.Uploaded,
            AvatarUrls.For(user.Id)));
    }
}
