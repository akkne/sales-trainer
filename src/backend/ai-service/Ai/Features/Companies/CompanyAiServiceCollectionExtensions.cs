using Sellevate.Ai.Features.Companies.Services.Abstract;
using Sellevate.Ai.Features.Companies.Services.Implementation;

namespace Sellevate.Ai.Features.Companies;

/// <summary>
/// Registers all Companies-feature AI services (briefing generation, call-log parsing, persona
/// generation, readiness scoring). Originally named after the first feature added here (39.12,
/// briefing-only) and renamed once it grew to cover ParseLog/Persona/Readiness too (39.17 PR #24
/// review fast-follow) — kept as a single registration point rather than splitting into per-feature
/// extension methods for a handful of scoped services.
/// </summary>
public static class CompanyAiServiceCollectionExtensions
{
    public static IServiceCollection AddCompanyAiFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IBriefingService, BriefingService>();
        services.AddScoped<IParseLogService, ParseLogService>();
        services.AddScoped<IPersonaService, PersonaService>();
        services.AddScoped<IReadinessService, ReadinessService>();
        return services;
    }
}
