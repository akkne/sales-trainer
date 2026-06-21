using Sellevate.Notification.Features.Notifications.Emails.Templates;
using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Emails;

/// <summary>
/// Resolves a notification to its template and renders the email. Templates are indexed by
/// <see cref="NotificationType"/> at construction; an unmapped type falls back to the generic
/// template. The relative action path is rewritten to an absolute URL (against the frontend
/// origin) before the template runs, so templates only ever see ready-to-click links.
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

        // Last registration wins if two templates ever claim the same type.
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

    private string? ToAbsoluteUrl(string? actionPath)
    {
        if (string.IsNullOrWhiteSpace(actionPath))
        {
            return null;
        }

        // Already a fully-qualified URL (defensive — notifications normally carry relative paths).
        // Detect by scheme rather than Uri.TryCreate(Absolute), which treats a Unix-style
        // "/path" as an absolute file:// URI and would wrongly skip the rewrite.
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
