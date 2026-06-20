using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Auth;

public sealed class ExpiredRefreshTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredRefreshTokenCleanupService> logger) : BackgroundService
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
                logger.LogError(exception, "ExpiredRefreshTokenCleanupService run failed; will retry next interval");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var nowUtc = DateTime.UtcNow;
        var deletedCount = await databaseContext.RefreshTokens
            .Where(token => token.IsRevoked || token.ExpiresAt < nowUtc)
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation(
            "ExpiredRefreshTokenCleanupService removed {DeletedCount} expired or revoked refresh tokens.",
            deletedCount);
    }
}
