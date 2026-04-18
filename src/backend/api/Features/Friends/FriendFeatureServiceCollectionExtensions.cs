using SalesTrainer.Api.Features.Friends.Services.Abstract;
using SalesTrainer.Api.Features.Friends.Services.Implementation;

namespace SalesTrainer.Api.Features.Friends;

public static class FriendFeatureServiceCollectionExtensions
{
    public static IServiceCollection AddFriendFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IFriendService, FriendService>();
        return services;
    }
}
