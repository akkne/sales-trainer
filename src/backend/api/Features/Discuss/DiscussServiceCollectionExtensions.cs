using SalesTrainer.Api.Features.Discuss.Services.Abstract;
using SalesTrainer.Api.Features.Discuss.Services.Implementation;

namespace SalesTrainer.Api.Features.Discuss;

public static class DiscussServiceCollectionExtensions
{
    public static IServiceCollection AddDiscussFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IDiscussService, DiscussService>();
        return services;
    }
}
