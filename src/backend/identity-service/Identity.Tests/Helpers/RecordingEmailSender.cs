using System.Collections.Concurrent;
using Sellevate.Identity.Infrastructure.Email.Abstract;
using Sellevate.Identity.Infrastructure.Email.Models;

namespace Sellevate.Identity.Tests.Helpers;

public sealed class RecordingEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> SentMessages { get; } = new();

    public Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        SentMessages.Enqueue(message);
        return Task.CompletedTask;
    }
}
