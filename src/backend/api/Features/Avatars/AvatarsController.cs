using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.Avatars.Models;
using SalesTrainer.Api.Features.Avatars.Services.Abstract;

namespace SalesTrainer.Api.Features.Avatars;

[ApiController]
[Route("avatars")]
public sealed class AvatarsController(IAvatarService avatarService) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };

    [HttpPost]
    [Authorize]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<AvatarUploadResponseDto>> UploadAvatar(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var userId = ResolveUserId();
        if (userId is null)
            return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided or file is empty." });

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "File size exceeds the 5 MB limit." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { error = $"Extension '{ext}' is not allowed. Allowed: .png, .jpg, .jpeg, .webp." });

        await using var stream = file.OpenReadStream();
        await avatarService.UploadAvatarAsync(userId.Value, stream, file.FileName, cancellationToken);

        return Ok(new AvatarUploadResponseDto($"/avatars/{userId.Value}"));
    }

    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> ResetAvatar(CancellationToken cancellationToken)
    {
        var userId = ResolveUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            await avatarService.ResetToDefaultAsync(userId.Value, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{userId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvatar(Guid userId, CancellationToken cancellationToken)
    {
        var result = await avatarService.GetAvatarAsync(userId, cancellationToken);

        if (result is null)
            return NotFound();

        Response.Headers["Cache-Control"] = "public, max-age=300";
        return File(result.Value.Stream, result.Value.ContentType);
    }

    private Guid? ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
