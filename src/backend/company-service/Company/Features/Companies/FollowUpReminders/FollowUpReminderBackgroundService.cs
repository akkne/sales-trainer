using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sellevate.Company.Features.Companies.FollowUpReminders;

/// <summary>
/// Polls for due company follow-ups on a fixed interval (default 5 minutes, configurable via
/// <see cref="FollowUpReminderOptions.PollIntervalMinutes"/>) and publishes
/// <c>company.followup.due</c> for each one via the scoped <see cref="IFollowUpReminderService"/>.
/// </summary>
internal sealed class FollowUpReminderBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<FollowUpReminderOptions> options,
    ILogger<FollowUpReminderBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.PollIntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await ProcessDueFollowUpsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Follow-up reminder poll failed; will retry next tick");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessDueFollowUpsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var reminderService = scope.ServiceProvider.GetRequiredService<IFollowUpReminderService>();

        var publishedCount = await reminderService.ProcessDueFollowUpsAsync(cancellationToken);
        if (publishedCount > 0)
        {
            logger.LogInformation("Published {Count} due company follow-up reminder(s)", publishedCount);
        }
    }
}
