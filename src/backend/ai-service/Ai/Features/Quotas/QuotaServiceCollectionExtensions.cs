using Sellevate.Ai.Features.Quotas.Services.Abstract;
using Sellevate.Ai.Features.Quotas.Services.Implementation;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Quotas;

public static class QuotaServiceCollectionExtensions
{
    public static IServiceCollection AddQuotaFeatureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AiQuotaConfiguration>(configuration.GetSection(AiQuotaConfiguration.SectionName));

        // The meter reads the workload class off the request header, and a message handler is not a
        // place a scoped ITenantContext can be reached from — so the accessor, rather than moving
        // the class into every method signature that would then need threading through four layers.
        services.AddHttpContextAccessor();

        services.AddScoped<IAiQuotaService, AiQuotaService>();
        services.AddScoped<IAiSpendMeter, AiSpendMeter>();

        return services;
    }
}
