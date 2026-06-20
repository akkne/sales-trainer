using Sellevate.Identity.Infrastructure.Email.Models;

namespace Sellevate.Identity.Infrastructure.Email.Abstract;

public interface IEmailSender
{
    Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
