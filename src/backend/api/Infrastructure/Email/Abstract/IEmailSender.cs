using SalesTrainer.Api.Infrastructure.Email.Models;

namespace SalesTrainer.Api.Infrastructure.Email.Abstract;

public interface IEmailSender
{
    Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
