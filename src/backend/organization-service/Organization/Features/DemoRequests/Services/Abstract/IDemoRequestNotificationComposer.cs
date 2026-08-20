using Sellevate.BuildingBlocks.Email.Models;
using Sellevate.Organization.Features.DemoRequests.Models;

namespace Sellevate.Organization.Features.DemoRequests.Services.Abstract;

/// <summary>
/// Builds the three emails a demo request can trigger. Kept apart from <c>DemoRequestService</c> so
/// each message body's shape can be unit-tested without a database or an
/// <see cref="Sellevate.BuildingBlocks.Email.Abstract.IEmailSender"/>.
/// </summary>
public interface IDemoRequestNotificationComposer
{
    /// <summary>
    /// The internal sales-inbox notification. The only email of the three that does not go to the
    /// submitter — see <see cref="ComposeSubmissionAcknowledgement"/> and
    /// <see cref="ComposeApprovalNotification"/> for the ones that do.
    /// </summary>
    EmailMessage ComposeInternalNotification(DemoRequest demoRequest, string recipientEmail, string recipientName);

    /// <summary>
    /// Sent to <see cref="DemoRequest.WorkEmail"/> the moment a lead is submitted: confirms the
    /// request arrived, sets the one-business-day expectation, and restates what the demo covers.
    /// </summary>
    EmailMessage ComposeSubmissionAcknowledgement(DemoRequest demoRequest);

    /// <summary>
    /// Sent to <see cref="DemoRequest.WorkEmail"/> on an actual transition into
    /// <see cref="Models.DemoRequestStatus.Approved"/>: tells the submitter access was approved and
    /// points them at registration.
    /// </summary>
    EmailMessage ComposeApprovalNotification(DemoRequest demoRequest);
}
