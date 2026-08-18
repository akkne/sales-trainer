namespace Sellevate.Identity.Features.Avatars.Services.Abstract;

public sealed record AvatarContentResult(Stream? Stream, string ContentType, string? ETag, bool NotModified);

/// <summary>
/// Reads and replaces a user's avatar. <see langword="null"/> from
/// <see cref="GetAvatarAsync"/> means "no image to serve" — unknown user, or a stored object that has
/// gone missing — and is expected rather than exceptional. Resetting to default deletes the uploaded
/// object; failing to delete it does not fail the reset.
/// </summary>
public interface IAvatarService
{
    Task<AvatarContentResult?> GetAvatarAsync(
        Guid userId,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default);

    Task<string> UploadAvatarAsync(
        Guid userId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default);

    Task ResetToDefaultAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
