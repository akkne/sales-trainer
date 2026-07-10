using Microsoft.Extensions.Configuration;
using Sellevate.Company.Features.Companies.FollowUpReminders;
using Sellevate.Company.Features.Companies.Services.Abstract;
using Sellevate.Company.Features.Companies.Services.Implementation;
using Sellevate.Company.Infrastructure.Ai;

namespace Sellevate.Company.Features.Companies;

public static class CompanyFeatureServiceCollectionExtensions
{
    public static IServiceCollection AddCompanyFeatureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddBriefingAiClient(configuration);
        services.AddParseLogAiClient(configuration);
        return services;
    }

    /// <summary>
    /// Registers the follow-up reminder poll: <see cref="FollowUpReminderOptions"/> (bound from
    /// the <c>FollowUpReminder</c> config section), the scoped due-poll/claim/publish service, and
    /// the hosted background service that ticks it every <c>PollIntervalMinutes</c>.
    /// </summary>
    public static IServiceCollection AddCompanyFollowUpReminders(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FollowUpReminderOptions>(configuration.GetSection(FollowUpReminderOptions.SectionName));
        services.AddScoped<IFollowUpReminderService, FollowUpReminderService>();
        services.AddHostedService<FollowUpReminderBackgroundService>();
        return services;
    }
}
