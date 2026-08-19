using System.Net;
using Sellevate.BuildingBlocks.Email.Models;
using Sellevate.Organization.Features.DemoRequests.Constants;
using Sellevate.Organization.Features.DemoRequests.Models;
using Sellevate.Organization.Features.DemoRequests.Services.Abstract;

namespace Sellevate.Organization.Features.DemoRequests.Services.Implementation;

/// <summary>
/// Writes the internal notification a submitted lead triggers. This is the only email
/// <c>DemoRequestService</c> ever sends — nothing goes to the submitter's own address, because a
/// public unauthenticated endpoint that mails whatever address it is handed would be an open mail
/// relay wearing a lead-capture form as a disguise.
/// </summary>
internal sealed class DemoRequestNotificationComposer : IDemoRequestNotificationComposer
{
    public EmailMessage Compose(DemoRequest demoRequest, string recipientEmail, string recipientName)
    {
        ArgumentNullException.ThrowIfNull(demoRequest);

        var escapedFullName = WebUtility.HtmlEncode(demoRequest.FullName);
        var escapedWorkEmail = WebUtility.HtmlEncode(demoRequest.WorkEmail);
        var escapedCompanyName = WebUtility.HtmlEncode(demoRequest.CompanyName);
        var escapedPhone = WebUtility.HtmlEncode(demoRequest.Phone ?? "-");
        var escapedJobTitle = WebUtility.HtmlEncode(demoRequest.JobTitle ?? "-");
        var escapedComment = WebUtility.HtmlEncode(demoRequest.Comment ?? "-");
        var marketingConsentAnswer = demoRequest.MarketingConsentGivenAt is null ? "No" : "Yes";

        var textBody =
            "New demo request\n\n" +
            $"Name: {demoRequest.FullName}\n" +
            $"Work email: {demoRequest.WorkEmail}\n" +
            $"Phone: {demoRequest.Phone ?? "-"}\n" +
            $"Company: {demoRequest.CompanyName}\n" +
            $"Job title: {demoRequest.JobTitle ?? "-"}\n" +
            $"Sales team size: {demoRequest.SalesTeamSize}\n" +
            $"Comment: {demoRequest.Comment ?? "-"}\n" +
            $"Marketing consent: {marketingConsentAnswer}\n";

        var htmlBody =
            "<p>New demo request</p>" +
            "<ul>" +
            $"<li><strong>Name:</strong> {escapedFullName}</li>" +
            $"<li><strong>Work email:</strong> {escapedWorkEmail}</li>" +
            $"<li><strong>Phone:</strong> {escapedPhone}</li>" +
            $"<li><strong>Company:</strong> {escapedCompanyName}</li>" +
            $"<li><strong>Job title:</strong> {escapedJobTitle}</li>" +
            $"<li><strong>Sales team size:</strong> {demoRequest.SalesTeamSize}</li>" +
            $"<li><strong>Comment:</strong> {escapedComment}</li>" +
            $"<li><strong>Marketing consent:</strong> {marketingConsentAnswer}</li>" +
            "</ul>";

        return new EmailMessage(
            RecipientEmail: recipientEmail,
            RecipientName: recipientName,
            Subject: DemoRequestNotificationConstants.EmailSubject,
            HtmlBody: htmlBody,
            TextBody: textBody);
    }
}
