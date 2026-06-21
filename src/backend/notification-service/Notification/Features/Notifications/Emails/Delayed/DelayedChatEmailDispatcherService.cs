using Microsoft.Extensions.Options;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Infrastructure.Configuration;

namespace Sellevate.Notification.Features.Notifications.Emails.Delayed;

/// <summary>
/// Background loop that flushes due unread-chat emails. On each tick it claims the messages whose
/// grace period has elapsed and, for those still unread (no read receipt has caught up), sends the
/// email. Messages already read are silently dropped — that is the whole point of the delay.
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

        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationEmailDispatcher>();

        foreach (var pending in due)
        {
            if (await _scheduler.WasReadAsync(pending, cancellationToken))
            {
                _logger.LogDebug(
                    "Skipping unread-chat email for {RecipientUserId}: conversation already read",
                    pending.RecipientUserId);
                continue;
            }

            await dispatcher.DispatchAsync(
                pending.RecipientUserId,
                NotificationType.ChatMessageReceived,
                NotificationTitles.ChatMessageReceived,
                pending.Body,
                pending.ActionUrl,
                cancellationToken);
        }
    }
}
