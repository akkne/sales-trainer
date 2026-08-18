using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Features.Avatars.Models;
using Sellevate.Identity.Features.Avatars.Services.Abstract;

namespace Sellevate.Identity.Features.Avatars;

/// <summary>
/// Serves and replaces avatar images. Reading an avatar is anonymous on purpose — the images are
/// rendered in shared views and carry nothing private — while writing requires the caller's own token
/// and can only touch their own avatar.
///
/// <para>
/// An upload has to pass both checks: an allow-listed extension and matching magic bytes. The extension
/// alone is a claim by the client, and the object store serves what it is given, so a mislabelled file
/// would otherwise be handed back with a content type it does not match.
/// </para>
/// </summary>
[ApiController]
[Route("avatars")]
public sealed class AvatarsController(IAvatarService avatarService) : ControllerBase
{
    private const int MaximumAvatarByteCount = 5 * 1024 * 1024;
    private const string AvatarCacheControlValue = "public, no-cache";
    private const string NoSniffValue = "nosniff";
    private const int MaximumAvatarMegabytes = 5;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] RiffHeader = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] WebpMarker = [0x57, 0x45, 0x42, 0x50];

    private static bool HasValidImageMagicBytes(byte[] header)
    {
        if (header.Length >= 4 && header[0] == PngSignature[0] && header[1] == PngSignature[1]
                && header[2] == PngSignature[2] && header[3] == PngSignature[3])
        {
            return true;
        }

        if (header.Length >= 3 && header[0] == JpegSignature[0] && header[1] == JpegSignature[1]
                && header[2] == JpegSignature[2])
        {
            return true;
        }

        if (header.Length >= 12
                && header[0] == RiffHeader[0] && header[1] == RiffHeader[1]
                && header[2] == RiffHeader[2] && header[3] == RiffHeader[3]
                && header[8] == WebpMarker[0] && header[9] == WebpMarker[1]
                && header[10] == WebpMarker[2] && header[11] == WebpMarker[3])
        {
            return true;
        }

        return false;
    }

    [HttpPost]
    [Authorize]
    [RequestSizeLimit(MaximumAvatarByteCount)]
    public async Task<ActionResult<AvatarUploadResponseDto>> UploadAvatar(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var userId = ResolveUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided or file is empty." });
        }

        if (file.Length > MaximumAvatarByteCount)
        {
            return BadRequest(new { error = $"File size exceeds the {MaximumAvatarMegabytes} MB limit." });
        }

        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(fileExtension))
        {
            return BadRequest(new { error = $"Extension '{fileExtension}' is not allowed. Allowed: .png, .jpg, .jpeg, .webp." });
        }

        await using var stream = file.OpenReadStream();
        const int imageMagicByteHeaderLength = 12;
        var header = new byte[imageMagicByteHeaderLength];
        var headerBytesRead = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        if (headerBytesRead < header.Length || !HasValidImageMagicBytes(header))
        {
            return BadRequest(new { error = "File content does not match an allowed image type (PNG, JPEG, or WebP)." });
        }

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
        {
            return Unauthorized();
        }

        try
        {
            await avatarService.ResetToDefaultAsync(userId.Value, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
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
        {
            return NotFound();
        }

        Response.Headers.CacheControl = AvatarCacheControlValue;
        Response.Headers.XContentTypeOptions = NoSniffValue;
        if (result.ETag is not null)
        {
            Response.Headers.ETag = result.ETag;
        }

        if (result.NotModified)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return File(result.Stream!, result.ContentType);
    }

    /// <summary>
    /// The caller's user id, or <see langword="null"/> when the token carries none. Reads
    /// <c>ClaimTypes.NameIdentifier</c> first and falls back to the raw <c>sub</c> spelling, because a
    /// principal built by the JWT handler carries the mapped URI while one forwarded by the gateway keeps
    /// the wire name.
    /// </summary>
    private Guid? ResolveUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypeNames.Subject);

        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
