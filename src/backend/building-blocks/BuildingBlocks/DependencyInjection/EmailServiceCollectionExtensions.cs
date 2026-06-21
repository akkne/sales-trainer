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
    public static IServiceCollection AddSellevateEmail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MailerSendConfiguration>(
            configuration.GetSection(MailerSendConfiguration.SectionName));

        services.AddHttpClient(MailerSendEmailSender.HttpClientName)
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30));

        services.AddScoped<IEmailSender, MailerSendEmailSender>();
        return services;
    }
}
