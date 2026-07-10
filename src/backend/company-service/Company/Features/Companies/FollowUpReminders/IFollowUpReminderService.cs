namespace Sellevate.Company.Features.Companies.FollowUpReminders;

public interface IFollowUpReminderService
{
    /// <summary>
    /// Claims and publishes <c>company.followup.due</c> for every company whose
    /// <c>NextActionAt</c> is due and not yet notified. Returns the number of reminders published.
    /// </summary>
    Task<int> ProcessDueFollowUpsAsync(CancellationToken cancellationToken = default);
}
