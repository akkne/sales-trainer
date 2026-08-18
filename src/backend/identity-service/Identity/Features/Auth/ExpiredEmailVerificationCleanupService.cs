using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Auth;

/// <summary>
/// Deletes expired email-verification codes once a day.
///
/// <para>
/// Phase 40.13: explicit <b>system mode</b>, for the same reason as
/// <see cref="ExpiredRefreshTokenCleanupService"/> — a verification code is attached to an email
/// address, which is an identity fact and not an organization's data
/// (docs/TENANCY/TENANCY.md §4.2). The mode is now declared rather than inherited from an empty
/// context, so an unset tenant can never be mistaken for "every tenant".
/// </para>
/// </summary>
public sealed class ExpiredEmailVerificationCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobConfiguration> backgroundJobOptions,
    ILogger<ExpiredEmailVerificationCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromHours(backgroundJobOptions.Value.ExpiredEmailVerificationCleanupIntervalHours));
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

    /// <summary>
    /// System mode is declared before the <see cref="IdentityDbContext"/> is resolved, so the
    /// context is built against a scope whose mode is already decided — the same ordering the
    /// sibling <see cref="ExpiredRefreshTokenCleanupService"/> depends on.
    /// </summary>
    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        scope.ServiceProvider.GetRequiredService<TenantContext>().EnterSystemMode();

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
