using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Auth;

public sealed class ExpiredEmailVerificationCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredEmailVerificationCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "ExpiredEmailVerificationCleanupService run failed; will retry next interval");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var nowUtc = DateTime.UtcNow;
        var deletedCount = await databaseContext.EmailVerificationCodes
            .Where(verificationCode => verificationCode.ExpiresAt < nowUtc)
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation(
            "ExpiredEmailVerificationCleanupService removed {DeletedCount} expired verification codes.",
            deletedCount);
    }
}
