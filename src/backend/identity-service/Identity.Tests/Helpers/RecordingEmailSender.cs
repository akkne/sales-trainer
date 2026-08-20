using System.Collections.Concurrent;
using Sellevate.BuildingBlocks.Email.Abstract;
using Sellevate.BuildingBlocks.Email.Models;

namespace Sellevate.Identity.Tests.Helpers;

public sealed class RecordingEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> SentMessages { get; } = new();

    /// <summary>
    /// Set to make the next <see cref="SendEmailAsync"/> call throw instead of recording — used to
    /// simulate a MailerSend outage after an invite (or any other row) has already been committed.
    /// Reset to <see langword="null"/> automatically after it throws once, so a test does not have to
    /// remember to turn it back off.
    /// </summary>
    public Exception? ExceptionToThrowOnNextSend { get; set; }

    public Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrowOnNextSend is { } exceptionToThrow)
        {
            ExceptionToThrowOnNextSend = null;
            throw exceptionToThrow;
        }

        SentMessages.Enqueue(message);
        return Task.CompletedTask;
    }
}
