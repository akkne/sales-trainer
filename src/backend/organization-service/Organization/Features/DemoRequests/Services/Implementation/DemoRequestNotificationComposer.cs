using System.Net;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Email.Models;
using Sellevate.Organization.Features.DemoRequests.Constants;
using Sellevate.Organization.Features.DemoRequests.Models;
using Sellevate.Organization.Features.DemoRequests.Services.Abstract;
using Sellevate.Organization.Infrastructure.Configuration;

namespace Sellevate.Organization.Features.DemoRequests.Services.Implementation;

/// <summary>
/// Writes the three emails a demo request can trigger: the internal sales-inbox notification, and —
/// since the 2026-08-20 reversal of the original "never mail the submitter" decision (docs/DECISIONS.md,
/// docs/DEMO_REQUEST.md) — the submitter's own submission acknowledgement and approval notification.
/// </summary>
internal sealed class DemoRequestNotificationComposer(
    IOptions<FrontendConfiguration> frontendOptions) : IDemoRequestNotificationComposer
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

        var registrationUrl = $"{frontendOptions.Value.PrimaryUrl.TrimEnd('/')}/register";
        var escapedFullName = WebUtility.HtmlEncode(demoRequest.FullName);
        var escapedCompanyName = WebUtility.HtmlEncode(demoRequest.CompanyName);
        var escapedRegistrationUrl = WebUtility.HtmlEncode(registrationUrl);

        var textBody =
            $"Здравствуйте, {demoRequest.FullName}!\n\n" +
            $"Рады сообщить: доступ к Sellevate для {demoRequest.CompanyName} одобрен. Чтобы начать " +
            "работу, зарегистрируйтесь и создайте логин и пароль по ссылке ниже:\n\n" +
            $"{registrationUrl}\n\n" +
            "До встречи в Sellevate!\nКоманда Sellevate";

        var htmlBody =
            $"<p>Здравствуйте, {escapedFullName}!</p>" +
            $"<p>Рады сообщить: доступ к Sellevate для {escapedCompanyName} одобрен. Чтобы начать " +
            $"работу, <a href=\"{escapedRegistrationUrl}\">зарегистрируйтесь и создайте логин и " +
            "пароль</a>.</p>" +
            "<p>До встречи в Sellevate!<br/>Команда Sellevate</p>";

        return new EmailMessage(
            RecipientEmail: demoRequest.WorkEmail,
            RecipientName: demoRequest.FullName,
            Subject: DemoRequestNotificationConstants.ApprovalEmailSubject,
            HtmlBody: htmlBody,
            TextBody: textBody);
    }
}
