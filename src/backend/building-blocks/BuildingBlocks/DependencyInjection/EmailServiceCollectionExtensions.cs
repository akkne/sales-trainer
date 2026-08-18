using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sellevate.BuildingBlocks.Email.Abstract;
using Sellevate.BuildingBlocks.Email.Configuration;
using Sellevate.BuildingBlocks.Email.Implementation;

namespace Sellevate.BuildingBlocks.DependencyInjection;

/// <summary>
/// Registers the shared outbound email stack (<see cref="IEmailSender"/> + its MailerSend
/// HTTP client and configuration) so any service can send transactional email with a single
/// <see cref="AddSellevateEmail"/> call.
/// </summary>
public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// Ceiling on one outbound MailerSend call. Sending is on the request path for invites and
    /// password resets, so a provider that accepts the connection and then stalls must fail rather
    /// than hold the caller open indefinitely.
    /// </summary>
    private static readonly TimeSpan MailerSendRequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Binds <see cref="MailerSendConfiguration"/> and registers the named HTTP client plus the
    /// scoped <see cref="IEmailSender"/>. Safe to call from a service that never sends email: an
    /// unconfigured token makes the sender a logging no-op rather than a startup failure.
    /// </summary>
    public static IServiceCollection AddSellevateEmail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MailerSendConfiguration>(
            configuration.GetSection(MailerSendConfiguration.SectionName));

        services.AddHttpClient(MailerSendEmailSender.HttpClientName)
            .ConfigureHttpClient(client => client.Timeout = MailerSendRequestTimeout);

        services.AddScoped<IEmailSender, MailerSendEmailSender>();
        return services;
    }
}
