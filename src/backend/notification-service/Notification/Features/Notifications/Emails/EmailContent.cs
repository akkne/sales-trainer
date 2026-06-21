namespace Sellevate.Notification.Features.Notifications.Emails;

/// <summary>
/// The rendered output of a notification email template: the subject line plus the HTML and
/// plain-text bodies. Mapped onto a transport <c>EmailMessage</c> by the dispatcher once a
/// recipient address is known.
/// </summary>
public sealed record EmailContent(string Subject, string HtmlBody, string TextBody);
