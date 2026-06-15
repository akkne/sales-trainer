using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SalesTrainer.Api.Infrastructure.Email.Abstract;
using SalesTrainer.Api.Infrastructure.Email.Models;

namespace SalesTrainer.Tests.Helpers;

public sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentDictionary<string, EmailMessage> _lastMessageByEmail = new();

    public Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _lastMessageByEmail[message.RecipientEmail.ToLowerInvariant()] = message;
        return Task.CompletedTask;
    }

    public string? GetLastCodeFor(string email)
    {
        if (!_lastMessageByEmail.TryGetValue(email.ToLowerInvariant(), out var message))
            return null;

        var match = Regex.Match(message.TextBody, @"\b\d{6}\b");
        return match.Success ? match.Value : null;
    }
}
