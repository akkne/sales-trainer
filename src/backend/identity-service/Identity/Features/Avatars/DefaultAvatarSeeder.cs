using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Features.Avatars.Models;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Infrastructure.Storage.Abstract;

namespace Sellevate.Identity.Features.Avatars;

/// <summary>
/// Uploads the stock avatar images shipped with the service and records them in the catalog, once per
/// startup. Best-effort by design: a missing asset file or an unreachable object store is logged and
/// skipped rather than thrown, because avatars are cosmetic and must not stop the service from booting.
/// A user whose index has no catalog row simply gets no picture.
/// </summary>
internal sealed class DefaultAvatarSeeder(
    IdentityDbContext database,
    IObjectStorage objectStorage,
    ILogger<DefaultAvatarSeeder> logger)
{
    public const int DefaultAvatarCount = 6;

    private static readonly string SeedAssetsDirectory =
        Path.Combine(AppContext.BaseDirectory, "SeedAssets");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        for (var catalogIndex = 0; catalogIndex < DefaultAvatarCount; catalogIndex++)
        {
            var fileName = AvatarObjectKeys.DefaultAvatarFileName(catalogIndex);
            var objectKey = AvatarObjectKeys.ForDefaultAvatar(catalogIndex);
            var localPath = Path.Combine(SeedAssetsDirectory, fileName);

            if (!File.Exists(localPath))
            {
                logger.LogWarning(
                    "DefaultAvatarSeeder: seed asset not found at {Path}, skipping index {Index}",
                    localPath, catalogIndex);
                continue;
            }

            bool objectReady;
            try
            {
                var exists = await objectStorage.ExistsAsync(objectKey, cancellationToken);
                if (!exists)
                {
                    await using var stream = File.OpenRead(localPath);
                    await objectStorage.PutAsync(
                        objectKey, stream, AvatarObjectKeys.DefaultAvatarContentType, cancellationToken);
                    logger.LogInformation("DefaultAvatarSeeder: uploaded {Key}", objectKey);
                }
                objectReady = true;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "DefaultAvatarSeeder: object store unreachable while seeding {Key}, skipping index {Index}",
                    objectKey, catalogIndex);
                objectReady = false;
            }

            if (!objectReady)
            {
                continue;
            }

            var existing = await database.DefaultAvatars
                .FirstOrDefaultAsync(candidate => candidate.Index == catalogIndex, cancellationToken);

            if (existing is null)
            {
                database.DefaultAvatars.Add(new DefaultAvatar
                {
                    Id = Guid.NewGuid(),
                    Index = catalogIndex,
                    ObjectKey = objectKey,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        logger.LogInformation("DefaultAvatarSeeder: completed seeding {Count} default avatars", DefaultAvatarCount);
    }
}
