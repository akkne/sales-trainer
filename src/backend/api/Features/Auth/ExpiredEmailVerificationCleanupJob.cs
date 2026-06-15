using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Auth;

public sealed class ExpiredEmailVerificationCleanupJob(
    AppDbContext databaseContext,
    ILogger<ExpiredEmailVerificationCleanupJob> logger)
{
    public async Task ExecuteAsync()
    {
        var nowUtc = DateTime.UtcNow;
        var deletedCount = await databaseContext.EmailVerificationCodes
            .Where(verificationCode => verificationCode.ExpiresAt < nowUtc)
            .ExecuteDeleteAsync();

        logger.LogInformation(
            "ExpiredEmailVerificationCleanupJob removed {DeletedCount} expired verification codes.",
            deletedCount);
    }
}
