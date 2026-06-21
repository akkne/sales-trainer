using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Sellevate.Identity.Features.Auth.Services.Implementation;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

[TestFixture]
public class EmailVerificationServiceHtmlEscapeTests
{
    private static EmailVerificationService Build(out RecordingEmailSender email)
    {
        var database = InMemoryDbContextFactory.Create();
        email = new RecordingEmailSender();
        var options = Options.Create(new EmailVerificationConfiguration());
        return new EmailVerificationService(database, email, options, NullLogger<EmailVerificationService>.Instance);
    }

    [Test]
    public async Task GenerateAndSendCode_HtmlBody_EscapesDisplayNameSpecialCharacters()
    {
        var service = Build(out var email);
        var maliciousName = "<script>alert('xss')</script>";

        await service.GenerateAndSendCodeAsync("user@test.com", maliciousName);

        var message = email.SentMessages.Single();
        message.HtmlBody.Should().NotContain("<script>", "raw HTML tags must be escaped in the HTML body");
        message.HtmlBody.Should().Contain("&lt;script&gt;", "display name must be HTML-encoded");
        // Text body should still contain the raw name (plain text, no escaping needed)
        message.TextBody.Should().Contain(maliciousName);
    }

    [Test]
    public async Task GenerateAndSendCode_HtmlBody_AmpersandIsEscaped()
    {
        var service = Build(out var email);

        await service.GenerateAndSendCodeAsync("user@test.com", "Alice & Bob");

        var message = email.SentMessages.Single();
        message.HtmlBody.Should().Contain("Alice &amp; Bob", "ampersands must be escaped in HTML body");
    }

    [Test]
    public async Task GenerateAndSendCode_HtmlBody_PlainNamePassesThrough()
    {
        var service = Build(out var email);

        await service.GenerateAndSendCodeAsync("user@test.com", "Alice");

        var message = email.SentMessages.Single();
        message.HtmlBody.Should().Contain("Hi Alice,", "safe display names must appear unchanged");
    }
}
