using SalesTrainer.Api.Features.Profile.Services.Abstract;
using SalesTrainer.Api.Features.Profile.Services.Implementation;

namespace SalesTrainer.Api.Features.Profile;

public static class ProfileServiceCollectionExtensions
{
    public static IServiceCollection AddProfileFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IProfileService, ProfileService>();
        return services;
    }
}
