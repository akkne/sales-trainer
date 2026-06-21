using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Email.Configuration;
using Sellevate.BuildingBlocks.Email.Implementation;
using Sellevate.BuildingBlocks.Email.Models;

namespace Sellevate.BuildingBlocks.Tests;

/// <summary>
/// Covers how <see cref="MailerSendEmailSender"/> decides whether it is configured: a placeholder
/// token no-ops quietly (local dev), a token with stray whitespace is flagged as a misconfiguration
/// (the real prod bug where the from-email/name were appended to the token line), and a valid token
/// is sent with a trimmed Bearer header.
/// </summary>
[TestFixture]
public sealed class MailerSendEmailSenderTests
{
    private static readonly EmailMessage Message =
        new("to@example.com", "To", "Subject", "<p>hi</p>", "hi");

    [Test]
    public async Task SendEmail_PlaceholderToken_NoOpsWithoutHttpCall()
    {
        var (factory, handler) = CreateFactory();
        var sender = CreateSender("YOUR_MAILERSEND_API_TOKEN", factory, out _);

        await sender.SendEmailAsync(Message);

        handler.CallCount.Should().Be(0);
    }

    [Test]
    public async Task SendEmail_TokenWithInternalWhitespace_IsTreatedAsMisconfiguredAndLogsError()
    {
        var (factory, handler) = CreateFactory();
        var sender = CreateSender(
            "mlsn.realtoken noreply@sellevate.site Sellevate", factory, out var logger);

        await sender.SendEmailAsync(Message);

        handler.CallCount.Should().Be(0);
        logger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Error && entry.Message.Contains("whitespace"));
    }

    [Test]
    public async Task SendEmail_ValidToken_SendsWithTrimmedBearerHeader()
    {
        var (factory, handler) = CreateFactory(HttpStatusCode.OK);
        var sender = CreateSender("  mlsn.validtoken  ", factory, out _);

        await sender.SendEmailAsync(Message);

        handler.CallCount.Should().Be(1);
        handler.LastAuthorization.Should().Be("Bearer mlsn.validtoken");
    }

    private static MailerSendEmailSender CreateSender(
        string apiToken, IHttpClientFactory factory, out CapturingLogger logger)
    {
        var options = Options.Create(new MailerSendConfiguration
        {
            ApiToken = apiToken,
            FromEmail = "noreply@sellevate.site",
        });
        logger = new CapturingLogger();
        return new MailerSendEmailSender(factory, options, logger);
    }

    private static (IHttpClientFactory Factory, RecordingHandler Handler) CreateFactory(
        HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new RecordingHandler(status);
        return (new SingleClientFactory(handler), handler);
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? LastAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastAuthorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class CapturingLogger : ILogger<MailerSendEmailSender>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
