namespace Sellevate.Organization.Infrastructure.Configuration;

/// <summary>
/// Tuning for the public "Request a demo" lead-capture endpoint: who gets notified, what name that
/// notification is addressed to, and how soon the same address may submit again.
/// <see cref="NotificationEmail"/> left blank is a deliberate, non-fatal state —
/// <c>DemoRequestService</c> still persists the lead and only skips sending the internal
/// notification, logging a warning instead, so an environment with no sales inbox configured yet
/// does not lose leads.
/// </summary>
public sealed class DemoRequestConfiguration
{
    public const string SectionName = "DemoRequests";

    public string NotificationEmail { get; init; } = string.Empty;

    public string NotificationRecipientName { get; init; } = "Sellevate";

    public int SubmissionCooldownSeconds { get; init; } = 300;
}
