using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Features.Avatars.Models;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Infrastructure.Storage.Abstract;

namespace Sellevate.Identity.Features.Avatars;

public sealed class DefaultAvatarSeeder(
    IdentityDbContext db,
    IObjectStorage objectStorage,
    ILogger<DefaultAvatarSeeder> logger)
{
    public const int DefaultAvatarCount = 6;

    private static readonly string SeedAssetsDirectory =
        Path.Combine(AppContext.BaseDirectory, "SeedAssets");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < DefaultAvatarCount; i++)
        {
            var fileName = $"avatar-{i:00}.png";
            var objectKey = $"defaults/{fileName}";
            var localPath = Path.Combine(SeedAssetsDirectory, fileName);

            if (!File.Exists(localPath))
            {
                logger.LogWarning("DefaultAvatarSeeder: seed asset not found at {Path}, skipping index {Index}", localPath, i);
                continue;
            }

            bool objectReady;
            try
            {
                var exists = await objectStorage.ExistsAsync(objectKey, cancellationToken);
                if (!exists)
                {
                    await using var stream = File.OpenRead(localPath);
                    await objectStorage.PutAsync(objectKey, stream, "image/png", cancellationToken);
                    logger.LogInformation("DefaultAvatarSeeder: uploaded {Key}", objectKey);
                }
                objectReady = true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "DefaultAvatarSeeder: object store unreachable while seeding {Key}, skipping index {Index}", objectKey, i);
                objectReady = false;
            }

            if (!objectReady)
                continue;

            var existing = await db.DefaultAvatars
                .FirstOrDefaultAsync(a => a.Index == i, cancellationToken);

            if (existing is null)
            {
                db.DefaultAvatars.Add(new DefaultAvatar
                {
                    Id = Guid.NewGuid(),
                    Index = i,
                    ObjectKey = objectKey,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("DefaultAvatarSeeder: completed seeding {Count} default avatars", DefaultAvatarCount);
    }
}
