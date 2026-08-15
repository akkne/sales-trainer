using Sellevate.Identity.Features.PlatformAdmin.Services.Abstract;
using Sellevate.Identity.Features.PlatformAdmin.Services.Implementation;
using Sellevate.Identity.Infrastructure.Configuration;

namespace Sellevate.Identity.Features.PlatformAdmin;

public static class PlatformAdminServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformAdminFeatureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ImpersonationConfiguration>(
            configuration.GetSection(ImpersonationConfiguration.SectionName));
        services.AddScoped<IPlatformAdminService, PlatformAdminService>();
        return services;
    }
}
