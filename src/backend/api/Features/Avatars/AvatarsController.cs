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

    // Magic-byte signatures for allowed image types
    private static readonly byte[] PngSignature  = [0x89, 0x50, 0x4E, 0x47];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] RiffHeader    = [0x52, 0x49, 0x46, 0x46]; // "RIFF"
    private static readonly byte[] WebpMarker    = [0x57, 0x45, 0x42, 0x50]; // "WEBP" at offset 8

    private static bool HasValidImageMagicBytes(byte[] header)
    {
        if (header.Length >= 4 && header[0] == PngSignature[0] && header[1] == PngSignature[1]
                && header[2] == PngSignature[2] && header[3] == PngSignature[3])
            return true;

        if (header.Length >= 3 && header[0] == JpegSignature[0] && header[1] == JpegSignature[1]
                && header[2] == JpegSignature[2])
            return true;

        // WebP: RIFF....WEBP
        if (header.Length >= 12
                && header[0] == RiffHeader[0] && header[1] == RiffHeader[1]
                && header[2] == RiffHeader[2] && header[3] == RiffHeader[3]
                && header[8] == WebpMarker[0] && header[9] == WebpMarker[1]
                && header[10] == WebpMarker[2] && header[11] == WebpMarker[3])
            return true;

        return false;
    }

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

        // Read leading bytes into a buffer for magic-byte validation, then rewind.
        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var headerRead = await stream.ReadAsync(header, 0, header.Length, cancellationToken);
        if (headerRead < 3 || !HasValidImageMagicBytes(header))
            return BadRequest(new { error = "File content does not match an allowed image type (PNG, JPEG, or WebP)." });
        stream.Seek(0, SeekOrigin.Begin);

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
        var ifNoneMatch = Request.Headers.IfNoneMatch.ToString();
        var result = await avatarService.GetAvatarAsync(
            userId,
            string.IsNullOrEmpty(ifNoneMatch) ? null : ifNoneMatch,
            cancellationToken);

        if (result is null)
            return NotFound();

        // no-cache = the client may cache but MUST revalidate via the ETag on every load.
        // This is what makes a freshly uploaded avatar appear after a page refresh while
        // still avoiding a full re-download (304) when the image is unchanged.
        Response.Headers["Cache-Control"] = "public, no-cache";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (result.ETag is not null)
            Response.Headers.ETag = result.ETag;

        if (result.NotModified)
            return StatusCode(StatusCodes.Status304NotModified);

        return File(result.Stream!, result.ContentType);
    }

    private Guid? ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
