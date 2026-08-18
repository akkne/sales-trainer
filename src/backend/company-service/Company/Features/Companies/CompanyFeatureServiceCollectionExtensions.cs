using Microsoft.Extensions.Configuration;
using Sellevate.Company.Features.Companies.FollowUpReminders;
using Sellevate.Company.Features.Companies.Services.Abstract;
using Sellevate.Company.Features.Companies.Services.Implementation;
using Sellevate.Company.Infrastructure.Ai;

namespace Sellevate.Company.Features.Companies;

/// <summary>
/// The company feature's two composition roots. Program.cs calls only these, so every lifetime
/// decision for the feature is visible in one place.
/// </summary>
public static class CompanyFeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the CRM service, its tuning (<see cref="CompanyServiceOptions"/>) and the four
    /// typed ai-service clients. All scoped: the service holds a <c>DbContext</c> whose tenant query
    /// filter closes over the request's organization, so a longer lifetime would outlive the tenant
    /// it was built for.
    /// </summary>
    public static IServiceCollection AddCompanyFeatureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CompanyServiceOptions>(configuration.GetSection(CompanyServiceOptions.SectionName));
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddBriefingAiClient(configuration);
        services.AddParseLogAiClient(configuration);
        services.AddPersonaAiClient(configuration);
        services.AddReadinessAiClient(configuration);
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
