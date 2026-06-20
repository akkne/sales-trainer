using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Avatars.Services.Abstract;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Infrastructure.Storage.Abstract;

namespace Sellevate.Identity.Features.Avatars.Services.Implementation;

public sealed class AvatarService(
    IdentityDbContext database,
    IObjectStorage objectStorage,
    IUserEventPublisher userEventPublisher,
    ILogger<AvatarService> logger) : IAvatarService
{
    private static readonly Dictionary<string, string> ContentTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp"
    };

    public async Task<AvatarContentResult?> GetAvatarAsync(
        Guid userId,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
    {
        var user = await database.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        string objectKey;

        if (user.AvatarType == AvatarKind.Uploaded && user.AvatarKey is not null)
        {
            objectKey = user.AvatarKey;
        }
        else
        {
            var defaultAvatar = await database.DefaultAvatars.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Index == user.DefaultAvatarIndex, cancellationToken);

            if (defaultAvatar is null)
            {
                return null;
            }

            objectKey = defaultAvatar.ObjectKey;
        }

        var fileExtension = Path.GetExtension(objectKey).ToLowerInvariant();
        var contentType = ContentTypeByExtension.GetValueOrDefault(fileExtension, "application/octet-stream");

        var etag = await objectStorage.TryGetETagAsync(objectKey, cancellationToken);

        if (etag is null)
        {
            return null;
        }

        if (ifNoneMatch is not null && ifNoneMatch == etag)
        {
            return new AvatarContentResult(null, contentType, etag, NotModified: true);
        }

        var stream = await objectStorage.GetAsync(objectKey, cancellationToken);
        return new AvatarContentResult(stream, contentType, etag, NotModified: false);
    }

    public async Task<string> UploadAvatarAsync(
        Guid userId,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = ContentTypeByExtension.GetValueOrDefault(fileExtension, "application/octet-stream");
        var objectKey = $"users/{userId}/avatar{fileExtension}";

        await objectStorage.PutAsync(objectKey, content, contentType, cancellationToken);

        var user = await database.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        user.AvatarType = AvatarKind.Uploaded;
        user.AvatarKey = objectKey;
        await database.SaveChangesAsync(cancellationToken);

        await userEventPublisher.PublishAvatarChangedAsync(
            new UserAvatarChangedEvent(userId, objectKey), cancellationToken);

        return objectKey;
    }

    public async Task ResetToDefaultAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await database.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (user.AvatarType == AvatarKind.Default && user.AvatarKey is null)
        {
            return;
        }

        if (user.AvatarKey is not null)
        {
            try
            {
                await objectStorage.DeleteAsync(user.AvatarKey, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to delete avatar object {Key} during reset", user.AvatarKey);
            }
        }

        user.AvatarType = AvatarKind.Default;
        user.AvatarKey = null;
        await database.SaveChangesAsync(cancellationToken);

        await userEventPublisher.PublishAvatarChangedAsync(
            new UserAvatarChangedEvent(userId, null), cancellationToken);
    }
}
