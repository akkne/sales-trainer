namespace Sellevate.Company.Features.Companies.FollowUpReminders;

public interface IFollowUpReminderService
{
    /// <summary>
    /// Claims and publishes <c>company.followup.due</c> for every company whose
    /// <c>NextActionAt</c> is due and not yet notified <b>within the current organization</b>.
    /// Returns the number of reminders published.
    ///
    /// <para>
    /// Phase 40.12: the organization comes from the ambient <c>ITenantContext</c>, which
    /// <see cref="FollowUpReminderBackgroundService"/> sets once per organization per tick. Throws
    /// if the tenant is unset or in system mode — the scan this method used to perform across every
    /// organization is exactly the failure mode the tenancy work exists to remove.
    /// </para>
    /// </summary>
    Task<int> ProcessDueFollowUpsAsync(CancellationToken cancellationToken = default);
}
