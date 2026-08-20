using System.Net;
using Sellevate.BuildingBlocks.Email.Models;
using Sellevate.Organization.Features.DemoRequests.Constants;
using Sellevate.Organization.Features.DemoRequests.Models;
using Sellevate.Organization.Features.DemoRequests.Services.Abstract;

namespace Sellevate.Organization.Features.DemoRequests.Services.Implementation;

/// <summary>
/// Writes the three emails a demo request can trigger: the internal sales-inbox notification, and —
/// since the 2026-08-20 reversal of the original "never mail the submitter" decision (docs/DECISIONS.md,
/// docs/DEMO_REQUEST.md) — the submitter's own submission acknowledgement and approval notification.
///
/// <para>
/// <b>The approval notification no longer promises a <c>/register</c> link.</b> Until provisioning
/// existed, <c>/register</c> was the only thing this email could point at, and it strands the
/// recipient on the awaiting-organization gate because <c>/register</c> creates a global identity
/// with no membership. Since a real invite link only exists once a platform superadmin actually
/// provisions the lead — a separate act, possibly minutes or days later — this email says the request
/// is approved and that the workspace invitation will follow, rather than link anywhere at all.
/// </para>
/// </summary>
internal sealed class DemoRequestNotificationComposer : IDemoRequestNotificationComposer
{
    public EmailMessage ComposeInternalNotification(DemoRequest demoRequest, string recipientEmail, string recipientName)
    {
        ArgumentNullException.ThrowIfNull(demoRequest);

        var escapedFullName = WebUtility.HtmlEncode(demoRequest.FullName);
        var escapedWorkEmail = WebUtility.HtmlEncode(demoRequest.WorkEmail);
        var escapedCompanyName = WebUtility.HtmlEncode(demoRequest.CompanyName);
        var escapedPhone = WebUtility.HtmlEncode(demoRequest.Phone);
        var escapedJobTitle = WebUtility.HtmlEncode(demoRequest.JobTitle ?? "-");
        var escapedComment = WebUtility.HtmlEncode(demoRequest.Comment ?? "-");
        var marketingConsentAnswer = demoRequest.MarketingConsentGivenAt is null ? "No" : "Yes";

        var textBody =
            "New demo request\n\n" +
            $"Name: {demoRequest.FullName}\n" +
            $"Work email: {demoRequest.WorkEmail}\n" +
            $"Phone: {demoRequest.Phone}\n" +
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
            Subject: DemoRequestNotificationConstants.InternalNotificationEmailSubject,
            HtmlBody: htmlBody,
            TextBody: textBody);
    }

    public EmailMessage ComposeSubmissionAcknowledgement(DemoRequest demoRequest)
    {
        ArgumentNullException.ThrowIfNull(demoRequest);

        var escapedFullName = WebUtility.HtmlEncode(demoRequest.FullName);

        var textBody =
            $"Здравствуйте, {demoRequest.FullName}!\n\n" +
            "Благодарим вас за интерес к Sellevate. Мы получили вашу заявку на демонстрацию и уже " +
            "передали её менеджеру — он свяжется с вами в течение одного рабочего дня.\n\n" +
            "Напомним, что вас ждёт на демонстрации: мы покажем, как Sellevate помогает отделу продаж " +
            "тренироваться на реалистичных диалогах, получать обратную связь по звонкам и быстрее " +
            "выходить на плановые показатели.\n\n" +
            "До встречи!\nКоманда Sellevate";

        var htmlBody =
            $"<p>Здравствуйте, {escapedFullName}!</p>" +
            "<p>Благодарим вас за интерес к Sellevate. Мы получили вашу заявку на демонстрацию и уже " +
            "передали её менеджеру — он свяжется с вами в течение одного рабочего дня.</p>" +
            "<p>Напомним, что вас ждёт на демонстрации: мы покажем, как Sellevate помогает отделу " +
            "продаж тренироваться на реалистичных диалогах, получать обратную связь по звонкам и " +
            "быстрее выходить на плановые показатели.</p>" +
            "<p>До встречи!<br/>Команда Sellevate</p>";

        return new EmailMessage(
            RecipientEmail: demoRequest.WorkEmail,
            RecipientName: demoRequest.FullName,
            Subject: DemoRequestNotificationConstants.SubmissionAcknowledgementEmailSubject,
            HtmlBody: htmlBody,
            TextBody: textBody);
    }

    public EmailMessage ComposeApprovalNotification(DemoRequest demoRequest)
    {
        ArgumentNullException.ThrowIfNull(demoRequest);

        var escapedFullName = WebUtility.HtmlEncode(demoRequest.FullName);
        var escapedCompanyName = WebUtility.HtmlEncode(demoRequest.CompanyName);

        var textBody =
            $"Здравствуйте, {demoRequest.FullName}!\n\n" +
            $"Рады сообщить: доступ к Sellevate для {demoRequest.CompanyName} одобрен. В ближайшее " +
            "время на этот адрес придёт отдельное письмо с приглашением в ваше рабочее пространство — " +
            "по нему вы сможете задать пароль и войти.\n\n" +
            "До встречи в Sellevate!\nКоманда Sellevate";

        var htmlBody =
            $"<p>Здравствуйте, {escapedFullName}!</p>" +
            $"<p>Рады сообщить: доступ к Sellevate для {escapedCompanyName} одобрен. В ближайшее время " +
            "на этот адрес придёт отдельное письмо с приглашением в ваше рабочее пространство — по нему " +
            "вы сможете задать пароль и войти.</p>" +
            "<p>До встречи в Sellevate!<br/>Команда Sellevate</p>";

        return new EmailMessage(
            RecipientEmail: demoRequest.WorkEmail,
            RecipientName: demoRequest.FullName,
            Subject: DemoRequestNotificationConstants.ApprovalEmailSubject,
            HtmlBody: htmlBody,
            TextBody: textBody);
    }
}
