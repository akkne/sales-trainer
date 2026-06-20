using Sellevate.Social.Features.Discuss.Services.Abstract;
using Sellevate.Social.Features.Discuss.Services.Implementation;

namespace Sellevate.Social.Features.Discuss;

public static class DiscussServiceCollectionExtensions
{
    public static IServiceCollection AddDiscussFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IDiscussService, DiscussService>();
        return services;
    }
}
