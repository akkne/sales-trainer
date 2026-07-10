namespace Sellevate.Company.Eventing;

/// <summary>
/// Published by the follow-up reminder background service when a company's scheduled
/// <c>NextActionAt</c> becomes due. Consumed by notification-service to create an in-app
/// "CompanyFollowUpDue" notification. Field names are the wire contract — keep in sync with
/// <c>NotificationIntegrationEvents.CompanyFollowUpDueEvent</c> in notification-service.
/// </summary>
public sealed record CompanyFollowUpDueEvent(
    Guid CompanyId,
    Guid UserId,
    string CompanyName,
    DateTime NextActionAt,
    string? Note);
