using Sellevate.Ai.Features.Companies.Services.Abstract;
using Sellevate.Ai.Features.Companies.Services.Implementation;

namespace Sellevate.Ai.Features.Companies;

/// <summary>
/// Registers all Companies-feature AI services (briefing generation + call-log parsing). Named
/// after the first feature added here (39.12); kept as a single registration point rather than
/// splitting into per-feature extension methods for a handful of scoped services.
/// </summary>
public static class BriefingServiceCollectionExtensions
{
    public static IServiceCollection AddBriefingFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IBriefingService, BriefingService>();
        services.AddScoped<IParseLogService, ParseLogService>();
        return services;
    }
}
