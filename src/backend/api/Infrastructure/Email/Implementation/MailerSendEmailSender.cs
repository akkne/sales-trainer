using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SalesTrainer.Api.Infrastructure.Configuration;
using SalesTrainer.Api.Infrastructure.Email.Abstract;
using SalesTrainer.Api.Infrastructure.Email.Models;

namespace SalesTrainer.Api.Infrastructure.Email.Implementation;

internal sealed class MailerSendEmailSender : IEmailSender
{
    public const string HttpClientName = "MailerSend";
    private const string SendEmailPath = "/v1/email";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<MailerSendConfiguration> _mailerSendOptions;
    private readonly ILogger<MailerSendEmailSender> _logger;

    public MailerSendEmailSender(
        IHttpClientFactory httpClientFactory,
        IOptions<MailerSendConfiguration> mailerSendOptions,
        ILogger<MailerSendEmailSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _mailerSendOptions = mailerSendOptions;
        _logger = logger;
    }

    private bool IsConfigured
    {
        get
        {
            var apiToken = _mailerSendOptions.Value.ApiToken;
            return !string.IsNullOrWhiteSpace(apiToken)
                && !apiToken.StartsWith("INJECTED", StringComparison.OrdinalIgnoreCase)
                && !apiToken.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var configuration = _mailerSendOptions.Value;

        if (!IsConfigured)
        {
            _logger.LogWarning(
                "MailerSend is not configured; skipping email to {RecipientEmail}. Subject: {Subject}. Body: {TextBody}",
                message.RecipientEmail,
                message.Subject,
                message.TextBody);
            return;
        }

        var requestPayload = new
        {
            from = new { email = configuration.FromEmail, name = configuration.FromName },
            to = new[] { new { email = message.RecipientEmail, name = message.RecipientName } },
            subject = message.Subject,
            text = message.TextBody,
            html = message.HtmlBody
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{configuration.BaseUrl.TrimEnd('/')}{SendEmailPath}")
        {
            Content = JsonContent.Create(requestPayload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration.ApiToken);

        var response = await _httpClientFactory.CreateClient(HttpClientName)
            .SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "MailerSend send failed with {StatusCode} for {RecipientEmail}: {ErrorContent}",
                response.StatusCode,
                message.RecipientEmail,
                errorContent);
            throw new InvalidOperationException(
                $"MailerSend returned {(int)response.StatusCode} while sending the email.");
        }

        _logger.LogInformation("Email sent to {RecipientEmail} via MailerSend", message.RecipientEmail);
    }
}
