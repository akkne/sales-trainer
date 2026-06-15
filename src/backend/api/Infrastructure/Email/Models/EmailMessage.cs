namespace SalesTrainer.Api.Infrastructure.Email.Models;

public sealed record EmailMessage(
    string RecipientEmail,
    string RecipientName,
    string Subject,
    string HtmlBody,
    string TextBody
);
