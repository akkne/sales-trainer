namespace Sellevate.Identity.Infrastructure.Configuration;

public sealed class MailerSendConfiguration
{
    public const string SectionName = "MailerSend";

    public required string ApiToken { get; init; }
    public string BaseUrl { get; init; } = "https://api.mailersend.com";
    public required string FromEmail { get; init; }
    public string FromName { get; init; } = "Sellevate";
}
