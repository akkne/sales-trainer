namespace SalesTrainer.Api.Features.Avatars.Services.Abstract;

public interface IAvatarService
{
    /// <summary>
    /// Returns the S3 object stream and content-type for a user's current avatar.
    /// Returns null if no matching default avatar row exists (catalog not seeded yet).
    /// </summary>
    Task<(Stream Stream, string ContentType)?> GetAvatarAsync(
        Guid userId,
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
