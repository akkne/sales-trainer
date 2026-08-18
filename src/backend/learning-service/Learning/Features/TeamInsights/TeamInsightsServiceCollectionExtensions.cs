using Sellevate.Learning.Features.TeamInsights.Services.Abstract;
using Sellevate.Learning.Features.TeamInsights.Services.Implementation;

namespace Sellevate.Learning.Features.TeamInsights;

/// <summary>
/// Registers the team-insight reads: the skill heat map, and the gap panel derived from it. Both are
/// <c>Scoped</c> because both read a tenant-scoped <c>LearningDbContext</c> (CODESTYLE §4).
/// </summary>
public static class TeamInsightsServiceCollectionExtensions
{
    public static IServiceCollection AddTeamInsightsFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<ITeamSkillMapService, TeamSkillMapService>();
        services.AddScoped<ITeamSkillGapService, TeamSkillGapService>();

        return services;
    }
}
