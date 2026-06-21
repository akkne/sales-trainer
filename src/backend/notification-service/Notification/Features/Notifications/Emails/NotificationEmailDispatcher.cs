using Sellevate.BuildingBlocks.Email.Abstract;
using Sellevate.BuildingBlocks.Email.Models;
using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Users;

namespace Sellevate.Notification.Features.Notifications.Emails;

/// <summary>
/// Default <see cref="INotificationEmailDispatcher"/>: looks the recipient up in the user
/// directory, renders the type-specific template and hands the message to the shared email sender.
/// Any failure is logged and swallowed — email is a best-effort side channel.
/// </summary>
internal sealed class NotificationEmailDispatcher : INotificationEmailDispatcher
{
    private readonly IUserDirectory _userDirectory;
    private readonly INotificationEmailRenderer _renderer;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<NotificationEmailDispatcher> _logger;

    public NotificationEmailDispatcher(
        IUserDirectory userDirectory,
        INotificationEmailRenderer renderer,
        IEmailSender emailSender,
        ILogger<NotificationEmailDispatcher> logger)
    {
        _userDirectory = userDirectory;
        _renderer = renderer;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task DispatchAsync(
        Guid recipientUserId,
        NotificationType notificationType,
        string title,
        string body,
        string? actionUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var recipient = await _userDirectory.GetAsync(recipientUserId, cancellationToken);
            if (recipient is null || string.IsNullOrWhiteSpace(recipient.Email))
            {
                _logger.LogDebug(
                    "No email on record for recipient {RecipientUserId}; skipping {NotificationType} email",
                    recipientUserId, notificationType);
                return;
            }

            var content = _renderer.Render(new NotificationEmailContext(
                recipient.DisplayName, notificationType, title, body, actionUrl));

            await _emailSender.SendEmailAsync(
                new EmailMessage(
                    recipient.Email,
                    string.IsNullOrWhiteSpace(recipient.DisplayName) ? recipient.Email : recipient.DisplayName,
                    content.Subject,
                    content.HtmlBody,
                    content.TextBody),
                cancellationToken);

            _logger.LogInformation(
                "Dispatched {NotificationType} email to recipient {RecipientUserId}",
                notificationType, recipientUserId);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to dispatch {NotificationType} email to recipient {RecipientUserId}",
                notificationType, recipientUserId);
        }
    }
}
