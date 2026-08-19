using Sellevate.BuildingBlocks.Email.Models;
using Sellevate.Organization.Features.DemoRequests.Models;

namespace Sellevate.Organization.Features.DemoRequests.Services.Abstract;

/// <summary>
/// Builds the internal sales-inbox notification for a submitted lead. Kept apart from
/// <c>DemoRequestService</c> so the message body's shape can be unit-tested without a database or an
/// <see cref="Sellevate.BuildingBlocks.Email.Abstract.IEmailSender"/>.
/// </summary>
public interface IDemoRequestNotificationComposer
{
    EmailMessage Compose(DemoRequest demoRequest, string recipientEmail, string recipientName);
}
