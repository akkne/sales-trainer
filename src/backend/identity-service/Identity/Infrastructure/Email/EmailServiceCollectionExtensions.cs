using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Email.Abstract;
using Sellevate.Identity.Infrastructure.Email.Implementation;

namespace Sellevate.Identity.Infrastructure.Email;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddEmailServices(
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
