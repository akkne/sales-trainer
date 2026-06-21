namespace Sellevate.BuildingBlocks.Email.Models;

/// <summary>
/// A single transactional email. <see cref="HtmlBody"/> carries the rich rendering and
/// <see cref="TextBody"/> the plain-text fallback; providers send both so the recipient's
/// client picks whichever it can render.
/// </summary>
public sealed record EmailMessage(
    string RecipientEmail,
    string RecipientName,
    string Subject,
    string HtmlBody,
    string TextBody
);
