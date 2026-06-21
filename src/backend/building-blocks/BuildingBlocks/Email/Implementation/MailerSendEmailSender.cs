using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Email.Abstract;
using Sellevate.BuildingBlocks.Email.Configuration;
using Sellevate.BuildingBlocks.Email.Models;

namespace Sellevate.BuildingBlocks.Email.Implementation;

/// <summary>
/// <see cref="IEmailSender"/> backed by the MailerSend transactional email API. When the
/// API token is not configured (placeholder value) the sender logs and returns instead of
/// throwing, so a missing credential never breaks a flow in local/dev.
/// </summary>
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

    /// <summary>The API token, trimmed of stray surrounding whitespace/newlines from env injection.</summary>
    private string? ApiToken => _mailerSendOptions.Value.ApiToken?.Trim();

    private static bool IsPlaceholder(string token) =>
        token.StartsWith("INJECTED", StringComparison.OrdinalIgnoreCase)
        || token.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);

    /// <summary>A real MailerSend token is a single opaque string — internal whitespace means the
    /// env value has extra content appended (e.g. from-email/name on the same line).</summary>
    private static bool HasInternalWhitespace(string token) => token.Any(char.IsWhiteSpace);

    private bool IsConfigured
    {
        get
        {
            var apiToken = ApiToken;
            return !string.IsNullOrWhiteSpace(apiToken)
                && !IsPlaceholder(apiToken)
                && !HasInternalWhitespace(apiToken);
        }
    }

    public async Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var configuration = _mailerSendOptions.Value;
        var apiToken = ApiToken;

        if (!IsConfigured)
        {
            // Distinguish a genuinely-unset placeholder (expected in local dev) from a malformed
            // token (a real misconfiguration that would otherwise silently 401), so the prod
            // symptom "emails don't arrive" maps to an actionable log line.
            if (!string.IsNullOrWhiteSpace(apiToken) && HasInternalWhitespace(apiToken) && !IsPlaceholder(apiToken))
            {
                _logger.LogError(
                    "MailerSend ApiToken contains whitespace — it likely has extra values appended " +
                    "(e.g. the from-email/name on the same env line). Set MAILERSEND_API_TOKEN to the " +
                    "token only. Skipping email to {RecipientEmail}.",
                    message.RecipientEmail);
            }
            else
            {
                _logger.LogWarning(
                    "MailerSend is not configured; skipping email to {RecipientEmail}. Subject: {Subject}. Body: {TextBody}",
                    message.RecipientEmail,
                    message.Subject,
                    message.TextBody);
            }
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
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

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
