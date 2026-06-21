using System.Net;
using System.Text;

namespace Sellevate.Notification.Features.Notifications.Emails;

/// <summary>
/// The shared visual chrome for every notification email: a responsive, inline-styled document
/// (email clients ignore &lt;style&gt; and external CSS, so all styling is inline) with the Sellevate
/// header, a content card, an optional call-to-action button and a footer. Templates supply only
/// their inner content; the layout guarantees a consistent, client-safe shell.
/// </summary>
public static class NotificationEmailLayout
{
    private const string BrandName = "Sellevate";
    private const string BackgroundColor = "#f4f5f7";
    private const string CardColor = "#ffffff";
    private const string AccentColor = "#4f46e5";
    private const string TextColor = "#1f2933";
    private const string MutedColor = "#7b8794";

    /// <summary>HTML-encodes untrusted text for safe inclusion in an HTML body.</summary>
    public static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>
    /// Builds a complete HTML email document. <paramref name="contentHtml"/> is the
    /// template-provided inner markup (already encoded by the caller); when
    /// <paramref name="actionUrl"/> and <paramref name="actionLabel"/> are both present a CTA
    /// button is rendered beneath the content.
    /// </summary>
    public static string Build(string headline, string contentHtml, string? actionUrl, string? actionLabel)
    {
        var builder = new StringBuilder();
        builder.Append(
            "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"></head>");
        builder.Append(
            $"<body style=\"margin:0;padding:0;background-color:{BackgroundColor};" +
            "font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;\">");

        builder.Append(
            $"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" " +
            $"style=\"background-color:{BackgroundColor};padding:24px 0;\"><tr><td align=\"center\">");

        builder.Append(
            "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" " +
            "style=\"max-width:560px;width:100%;\">");

        // Header
        builder.Append(
            $"<tr><td style=\"padding:8px 8px 16px;font-size:20px;font-weight:700;color:{AccentColor};\">" +
            $"{Encode(BrandName)}</td></tr>");

        // Card
        builder.Append(
            $"<tr><td style=\"background-color:{CardColor};border-radius:12px;padding:32px;" +
            "box-shadow:0 1px 3px rgba(0,0,0,0.08);\">");

        builder.Append(
            $"<h1 style=\"margin:0 0 16px;font-size:22px;line-height:1.3;color:{TextColor};\">" +
            $"{Encode(headline)}</h1>");

        builder.Append($"<div style=\"font-size:16px;line-height:1.6;color:{TextColor};\">{contentHtml}</div>");

        if (!string.IsNullOrWhiteSpace(actionUrl) && !string.IsNullOrWhiteSpace(actionLabel))
        {
            builder.Append(
                $"<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:28px 0 4px;\">" +
                $"<tr><td style=\"border-radius:8px;background-color:{AccentColor};\">" +
                $"<a href=\"{Encode(actionUrl)}\" style=\"display:inline-block;padding:12px 24px;" +
                "font-size:16px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:8px;\">" +
                $"{Encode(actionLabel)}</a></td></tr></table>");
        }

        builder.Append("</td></tr>");

        // Footer
        builder.Append(
            $"<tr><td style=\"padding:20px 8px;font-size:12px;line-height:1.5;color:{MutedColor};\">" +
            $"You're receiving this email because of activity on your {Encode(BrandName)} account." +
            "</td></tr>");

        builder.Append("</table></td></tr></table></body></html>");
        return builder.ToString();
    }
}
