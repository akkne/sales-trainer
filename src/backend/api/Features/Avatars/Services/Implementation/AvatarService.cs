using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Avatars.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Api.Infrastructure.Storage.Abstract;

namespace SalesTrainer.Api.Features.Avatars.Services.Implementation;

public sealed class AvatarService(
    AppDbContext db,
    IObjectStorage objectStorage,
    ILogger<AvatarService> logger) : IAvatarService
{
    private static readonly Dictionary<string, string> ContentTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"]  = "image/png",
        [".jpg"]  = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp"
    };

    public async Task<AvatarContentResult?> GetAvatarAsync(
        Guid userId,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return null;

        string objectKey;

        if (user.AvatarType == AvatarKind.Uploaded && user.AvatarKey is not null)
        {
            objectKey = user.AvatarKey;
        }
        else
        {
            var defaultAvatar = await db.DefaultAvatars.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Index == user.DefaultAvatarIndex, cancellationToken);

            if (defaultAvatar is null)
                return null;

            objectKey = defaultAvatar.ObjectKey;
        }

        var ext = Path.GetExtension(objectKey).ToLowerInvariant();
        var contentType = ContentTypeByExtension.GetValueOrDefault(ext, "application/octet-stream");

        // ETag drives cache validation: clients revalidate on every load (Cache-Control: no-cache)
        // and we serve a fresh image only when the stored object actually changed.
        var etag = await objectStorage.TryGetETagAsync(objectKey, cancellationToken);

        // Object key is in the DB but the underlying object is gone — treat as not found
        // (404) so the client falls back to the generated avatar instead of getting a 500.
        if (etag is null)
            return null;

        if (ifNoneMatch is not null && ifNoneMatch == etag)
            return new AvatarContentResult(null, contentType, etag, NotModified: true);

        var stream = await objectStorage.GetAsync(objectKey, cancellationToken);
        return new AvatarContentResult(stream, contentType, etag, NotModified: false);
    }

    public async Task<string> UploadAvatarAsync(
        Guid userId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = ContentTypeByExtension.GetValueOrDefault(ext, "application/octet-stream");
        var objectKey = $"users/{userId}/avatar{ext}";

        await objectStorage.PutAsync(objectKey, content, contentType, cancellationToken);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        user.AvatarType = AvatarKind.Uploaded;
        user.AvatarKey = objectKey;
        await db.SaveChangesAsync(cancellationToken);

        return objectKey;
    }

    public async Task ResetToDefaultAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (user.AvatarType == AvatarKind.Default && user.AvatarKey is null)
            return;

        if (user.AvatarKey is not null)
        {
            try
            {
                await objectStorage.DeleteAsync(user.AvatarKey, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete avatar object {Key} during reset", user.AvatarKey);
            }
        }

        user.AvatarType = AvatarKind.Default;
        user.AvatarKey = null;
        await db.SaveChangesAsync(cancellationToken);
    }
}
