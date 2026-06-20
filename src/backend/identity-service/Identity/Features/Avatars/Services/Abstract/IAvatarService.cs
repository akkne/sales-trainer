namespace Sellevate.Identity.Features.Avatars.Services.Abstract;

/// <summary>
/// Resolved avatar payload. When <see cref="NotModified"/> is true the caller's
/// If-None-Match matched the current ETag and <see cref="Stream"/> is null (send 304).
/// </summary>
public sealed record AvatarContentResult(Stream? Stream, string ContentType, string? ETag, bool NotModified);

public interface IAvatarService
{
    /// <summary>
    /// Returns the S3 object stream, content-type and ETag for a user's current avatar.
    /// Returns null if the user/avatar object cannot be resolved (catalog not seeded, object missing).
    /// When <paramref name="ifNoneMatch"/> matches the current ETag, returns a not-modified result
    /// with a null stream so the controller can answer 304.
    /// </summary>
    Task<AvatarContentResult?> GetAvatarAsync(
        Guid userId,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads the provided stream as the user's custom avatar.
    /// Returns the object key stored in S3.
    /// </summary>
    Task<string> UploadAvatarAsync(
        Guid userId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the user's avatar to the default (deletes the uploaded object if any).
    /// </summary>
    Task ResetToDefaultAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
