namespace Sellevate.Organization.Features.DemoRequests.Constants;

/// <summary>
/// Wording used by the three emails a demo request can trigger: the internal sales-inbox
/// notification, the submitter's own submission acknowledgement, and the submitter's approval
/// notification.
/// </summary>
public static class DemoRequestNotificationConstants
{
    public const string InternalNotificationEmailSubject = "New demo request";

    public const string SubmissionAcknowledgementEmailSubject = "Спасибо, что выбрали Sellevate";

    public const string ApprovalEmailSubject = "Заявку одобрили";
}
