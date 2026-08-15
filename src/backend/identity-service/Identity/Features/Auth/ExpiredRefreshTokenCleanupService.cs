using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Auth;

/// <summary>
/// Deletes revoked and expired refresh tokens once a day.
///
/// <para>
/// Phase 40.13 gave this job the second of the two legitimate background-job modes in
/// docs/TENANCY/TENANCY.md §1.6: <b>explicit system mode</b>. Refresh tokens belong to an identity,
/// and an identity in this product is cross-organization (§4.2) — <c>RefreshTokens</c> has no
/// <c>OrganizationId</c> column and no RLS policy, so there is nothing per-tenant to iterate. What
/// changed is not the SQL but the honesty of the scope: before, the job ran on a fresh
/// <see cref="TenantContext"/> that happened to be blank, which is indistinguishable at a glance
/// from "somebody forgot to set the tenant". Now it says so, and any future tenant-scoped table
/// reached from this scope will fail loudly instead of quietly deleting one organization's rows
/// under another's filter.
/// </para>
/// </summary>
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

        // Declared before the DbContext is resolved, so the context is built against a scope whose
        // mode is already decided. TenantContext refuses to be re-pointed afterwards, which is what
        // makes "system" a statement rather than a default.
        scope.ServiceProvider.GetRequiredService<TenantContext>().EnterSystemMode();

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
