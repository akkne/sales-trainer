using Sellevate.Notification.Features.Notifications.Emails.Templates;
using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Emails;

/// <summary>
/// Resolves a notification to its template and renders the email. Templates are indexed by
/// <see cref="NotificationType"/> at construction; an unmapped type falls back to the generic
/// template. The relative action path is rewritten to an absolute URL (against the frontend
/// origin) before the template runs, so templates only ever see ready-to-click links.
///
/// <para>
/// Two templates claiming the same <see cref="NotificationType"/> is a registration mistake and
/// fails fast at construction — that is, at application startup — rather than silently picking one.
/// </para>
/// </summary>
public sealed class NotificationEmailRenderer : INotificationEmailRenderer
{
    private readonly IReadOnlyDictionary<NotificationType, INotificationEmailTemplate> _templatesByType;
    private readonly INotificationEmailTemplate _fallbackTemplate;
    private readonly string _frontendBaseUrl;

    public NotificationEmailRenderer(
        IEnumerable<INotificationEmailTemplate> templates,
        GenericNotificationEmailTemplate fallbackTemplate,
        string frontendBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(fallbackTemplate);

        _templatesByType = templates.ToDictionary(template => template.NotificationType);
        _fallbackTemplate = fallbackTemplate;
        _frontendBaseUrl = (frontendBaseUrl ?? string.Empty).TrimEnd('/');
    }

    public EmailContent Render(NotificationEmailContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var resolved = context with { ActionUrl = ToAbsoluteUrl(context.ActionUrl) };
        var template = _templatesByType.GetValueOrDefault(resolved.NotificationType, _fallbackTemplate);
        return template.Render(resolved);
    }

    /// <summary>
    /// Rewrites a stored relative action path against the frontend origin, and passes through a
    /// path that is already fully qualified — defensively, since notifications normally carry
    /// relative paths. Detection is by scheme separator rather than
    /// <c>Uri.TryCreate(UriKind.Absolute)</c>, which treats a Unix-style "/path" as an absolute
    /// <c>file://</c> URI and would wrongly skip the rewrite, mailing a link to nowhere.
    /// </summary>
    private string? ToAbsoluteUrl(string? actionPath)
    {
        if (string.IsNullOrWhiteSpace(actionPath))
        {
            return null;
        }

        if (actionPath.Contains("://", StringComparison.Ordinal))
        {
            return actionPath;
        }

        if (string.IsNullOrEmpty(_frontendBaseUrl))
        {
            return actionPath;
        }

        return actionPath.StartsWith('/')
            ? _frontendBaseUrl + actionPath
            : $"{_frontendBaseUrl}/{actionPath}";
    }
}
