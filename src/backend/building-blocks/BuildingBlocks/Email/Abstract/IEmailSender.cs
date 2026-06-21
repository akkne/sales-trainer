using Sellevate.BuildingBlocks.Email.Models;

namespace Sellevate.BuildingBlocks.Email.Abstract;

/// <summary>
/// Provider-agnostic outbound email port. Implementations deliver an
/// <see cref="EmailMessage"/> through a concrete provider (e.g. MailerSend).
/// Shared across services via <c>Sellevate.BuildingBlocks</c> so the wiring and
/// provider configuration live in one place.
/// </summary>
public interface IEmailSender
{
    Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
