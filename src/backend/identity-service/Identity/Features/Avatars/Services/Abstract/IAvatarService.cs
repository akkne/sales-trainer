namespace Sellevate.Identity.Features.Avatars.Services.Abstract;

public sealed record AvatarContentResult(Stream? Stream, string ContentType, string? ETag, bool NotModified);

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
