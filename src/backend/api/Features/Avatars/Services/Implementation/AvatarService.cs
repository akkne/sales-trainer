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

    public async Task<(Stream Stream, string ContentType)?> GetAvatarAsync(
        Guid userId,
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

        var stream = await objectStorage.GetAsync(objectKey, cancellationToken);
        var ext = Path.GetExtension(objectKey).ToLowerInvariant();
        var contentType = ContentTypeByExtension.GetValueOrDefault(ext, "application/octet-stream");

        return (stream, contentType);
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
