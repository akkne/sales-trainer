using Sellevate.Ai.Features.Quotas.Services.Abstract;
using Sellevate.Ai.Features.Quotas.Services.Implementation;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Quotas;

/// <summary>
/// Registers the quota feature: the allowance reader/writer, the meter every provider call passes
/// through, and the handler that turns a refusal into a status code.
/// </summary>
public static class QuotaServiceCollectionExtensions
{
    /// <summary>
    /// <para>
    /// <see cref="Microsoft.Extensions.DependencyInjection.HttpServiceCollectionExtensions.AddHttpContextAccessor"/>
    /// is required rather than incidental: the meter reads the workload class off the request header,
    /// and a message handler is not a place a scoped <c>ITenantContext</c> can be reached from — so
    /// the accessor, rather than moving the class into every method signature that would then need
    /// threading through four layers.
    /// </para>
    /// </summary>
    public static IServiceCollection AddQuotaFeatureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AiQuotaConfiguration>(configuration.GetSection(AiQuotaConfiguration.SectionName));

        services.AddHttpContextAccessor();

        services.AddScoped<IAiQuotaService, AiQuotaService>();
        services.AddScoped<IAiSpendMeter, AiSpendMeter>();
        services.AddExceptionHandler<AiQuotaExceptionHandler>();

        return services;
    }
}
