using Sellevate.Social.Features.Discuss.Services.Abstract;
using Sellevate.Social.Features.Discuss.Services.Implementation;

namespace Sellevate.Social.Features.Discuss;

/// <summary>
/// Registers the discussion feature. Scoped, because the service reads the request's tenant through
/// its <c>DbContext</c> and <c>ITenantContext</c>.
/// </summary>
public static class DiscussServiceCollectionExtensions
{
    public static IServiceCollection AddDiscussFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IDiscussService, DiscussService>();
        return services;
    }
}
