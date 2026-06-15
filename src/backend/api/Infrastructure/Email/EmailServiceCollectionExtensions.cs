using SalesTrainer.Api.Infrastructure.Configuration;
using SalesTrainer.Api.Infrastructure.Email.Abstract;
using SalesTrainer.Api.Infrastructure.Email.Implementation;

namespace SalesTrainer.Api.Infrastructure.Email;

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
