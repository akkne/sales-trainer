using Sellevate.Identity.Features.Profile.Services.Abstract;
using Sellevate.Identity.Features.Profile.Services.Implementation;

namespace Sellevate.Identity.Features.Profile;

public static class ProfileServiceCollectionExtensions
{
    public static IServiceCollection AddProfileFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IProfileService, ProfileService>();
        return services;
    }
}
