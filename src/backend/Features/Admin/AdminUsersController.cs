using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Auth;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

public record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    DateTime CreatedAt
);

public record ChangeUserRoleRequestDto(string Role);

[ApiController]
[Route("admin/users")]
[Authorize(Policy = "RequireAdmin")]
public class AdminUsersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminUserDto>>> GetAll()
    {
        var users = await db.Users
            .OrderBy(u => u.CreatedAt)
            .Select(u => new AdminUserDto(
                u.Id, u.Email, u.DisplayName, u.Role.ToString(), u.CreatedAt))
            .ToListAsync();

        return Ok(users);
    }

    [HttpPut("{id:guid}/role")]
    [Authorize(Policy = "RequireSuperAdmin")]
    public async Task<ActionResult<AdminUserDto>> ChangeRole(
        Guid id, [FromBody] ChangeUserRoleRequestDto dto)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (!Enum.TryParse<UserRole>(dto.Role, ignoreCase: true, out var newRole))
            return BadRequest(new { message = $"Unknown role: {dto.Role}" });

        user.Role = newRole;
        await db.SaveChangesAsync();

        return Ok(new AdminUserDto(
            user.Id, user.Email, user.DisplayName, user.Role.ToString(), user.CreatedAt));
    }
}
