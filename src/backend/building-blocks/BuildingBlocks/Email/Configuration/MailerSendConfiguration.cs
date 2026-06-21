namespace Sellevate.BuildingBlocks.Email.Configuration;

/// <summary>
/// Binds the <c>MailerSend</c> configuration section. <see cref="ApiToken"/> and
/// <see cref="FromEmail"/> are injected from the environment per service; when the token
/// is left as a placeholder the sender no-ops (logs instead of calling the API), which keeps
/// local/dev environments runnable without credentials.
/// </summary>
public sealed class MailerSendConfiguration
{
    public const string SectionName = "MailerSend";

    public required string ApiToken { get; init; }
    public string BaseUrl { get; init; } = "https://api.mailersend.com";
    public required string FromEmail { get; init; }
    public string FromName { get; init; } = "Sellevate";
}
