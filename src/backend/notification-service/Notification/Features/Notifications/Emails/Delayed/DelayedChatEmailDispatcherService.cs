using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Infrastructure.Configuration;

namespace Sellevate.Notification.Features.Notifications.Emails.Delayed;

/// <summary>
/// Background loop that flushes due unread-chat emails. On each tick it claims the messages whose
/// grace period has elapsed and, for those still unread (no read receipt has caught up), sends the
/// email. Messages already read are silently dropped — that is the whole point of the delay.
///
/// <para>
/// Phase 40.13 made this the "iterate organizations" flavour of background job
/// (docs/TENANCY/TENANCY.md §1.6), driven by the claimed batch rather than by a registry: the
/// queue is cross-organization, so a batch can hold items from several customers, and each one
/// gets its own scope with its own organization set. One scope is never reused across two
/// organizations — <c>TenantContext</c> refuses to be re-pointed, which is what turns "the loop
/// forgot to reset the tenant" from a silent cross-tenant email into an exception.
/// </para>
/// </summary>
internal sealed class DelayedChatEmailDispatcherService : BackgroundService
{
    private const int MaxBatchSize = 100;

    private readonly IDelayedChatEmailScheduler _scheduler;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationEmailConfiguration _configuration;
    private readonly ILogger<DelayedChatEmailDispatcherService> _logger;

    public DelayedChatEmailDispatcherService(
        IDelayedChatEmailScheduler scheduler,
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationEmailConfiguration> configuration,
        ILogger<DelayedChatEmailDispatcherService> logger)
    {
        _scheduler = scheduler;
        _scopeFactory = scopeFactory;
        _configuration = configuration.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _configuration.DispatcherPollIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FlushDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unread-chat email flush failed; will retry next tick");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }

    private async Task FlushDueAsync(CancellationToken cancellationToken)
    {
        var due = await _scheduler.ClaimDueAsync(DateTime.UtcNow, MaxBatchSize, cancellationToken);
        if (due.Count == 0)
        {
            return;
        }

        // Per-item, not per-batch. ClaimDueAsync is destructive — it reads the due set and removes it
        // in one step — so this list is the only remaining copy of up to a hundred emails. With the
        // guard around the whole loop, one SMTP timeout on the third recipient unwound past the
        // ninety-seven behind it and logged "will retry next tick" with nothing left to retry.
        // Review, 40.34.
        foreach (var pending in due)
        {
            try
            {
                if (await _scheduler.WasReadAsync(pending, cancellationToken))
                {
                    _logger.LogDebug(
                        "Skipping unread-chat email for {RecipientUserId}: conversation already read",
                        pending.RecipientUserId);
                    continue;
                }

                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().SetOrganization(pending.OrganizationId);

                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationEmailDispatcher>();

                await dispatcher.DispatchAsync(
                    pending.RecipientUserId,
                    NotificationType.ChatMessageReceived,
                    NotificationTitles.ChatMessageReceived,
                    pending.Body,
                    pending.ActionUrl,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Failed to send the unread-chat email for {RecipientUserId}; continuing with the rest of the batch",
                    pending.RecipientUserId);
            }
        }
    }
}
