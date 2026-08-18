using Sellevate.Learning.Features.TeamInsights.Services.Abstract;
using Sellevate.Learning.Features.TeamInsights.Services.Implementation;

namespace Sellevate.Learning.Features.TeamInsights;

public static class TeamInsightsServiceCollectionExtensions
{
    public static IServiceCollection AddTeamInsightsFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<ITeamSkillMapService, TeamSkillMapService>();
        services.AddScoped<ITeamSkillGapService, TeamSkillGapService>();

        return services;
    }
}
