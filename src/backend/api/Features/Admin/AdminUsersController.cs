using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Avatars;
using SalesTrainer.Api.Features.Avatars.Services.Abstract;
using SalesTrainer.Api.Features.Profile.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

[ApiController]
[Route("admin/users")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminUsersController(
    AppDbContext database,
    IAvatarService avatarService,
    IProfileService profileService,
    ILogger<AdminUsersController> logger) : ControllerBase
{
    private const int DisplayNameMinLength = 2;
    private const int DisplayNameMaxLength = 50;

    [HttpGet]
    public async Task<ActionResult<List<AdminUserDto>>> GetAll()
    {
        var users = await database.Users
            .OrderBy(u => u.CreatedAt)
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email,
                u.DisplayName,
                u.Role.ToString(),
                u.CreatedAt,
                u.IsEmailVerified,
                u.GoogleId != null ? "Google" : "Password",
                u.AvatarType == AvatarKind.Uploaded,
                AvatarUrls.For(u.Id)))
            .ToListAsync();

        logger.LogInformation("Admin user list fetched by {ActorId}, count={Count}",
            User.FindFirstValue(ClaimTypes.NameIdentifier), users.Count);

        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminUserDetailDto>> GetById(
        Guid id, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null) return NotFound();

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

    /// <summary>
    /// Moderation: rename a user's display name (e.g. when a nickname is inappropriate).
    /// Available to any admin; role changes remain SuperAdmin-only.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminUserDto>> UpdateUser(
        Guid id, [FromBody] UpdateUserRequestDto dto, CancellationToken cancellationToken)
    {
        var displayName = (dto.DisplayName ?? "").Trim();
        if (displayName.Length is < DisplayNameMinLength or > DisplayNameMaxLength)
            return BadRequest(new
            {
                message = $"Display name must be between {DisplayNameMinLength} and {DisplayNameMaxLength} characters."
            });

        var user = await database.Users.FindAsync([id], cancellationToken);
        if (user is null) return NotFound();

        var previousName = user.DisplayName;
        user.DisplayName = displayName;
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User display name changed TargetUserId={TargetUserId} \"{OldName}\" → \"{NewName}\" by ActorId={ActorId}",
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

    /// <summary>
    /// Moderation: remove a user's uploaded avatar and fall back to the default
    /// (used when a user uploads an inappropriate photo).
    /// </summary>
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
        Guid id, [FromBody] ChangeUserRoleRequestDto dto)
    {
        var user = await database.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (!Enum.TryParse<UserRole>(dto.Role, ignoreCase: true, out var newRole))
            return BadRequest(new { message = $"Unknown role: {dto.Role}" });

        var previousRole = user.Role;
        user.Role = newRole;
        await database.SaveChangesAsync();

        logger.LogInformation("User role changed TargetUserId={TargetUserId} Email={Email} {OldRole} → {NewRole} by ActorId={ActorId}",
            user.Id, user.Email, previousRole, newRole,
            User.FindFirstValue(ClaimTypes.NameIdentifier));

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
