using Sellevate.Social.Features.Chat.Services.Abstract;
using Sellevate.Social.Features.Chat.Services.Implementation;
using Sellevate.Social.Features.Friends.Services.Abstract;
using Sellevate.Social.Features.Friends.Services.Implementation;

namespace Sellevate.Social.Features.Friends;

public static class FriendFeatureServiceCollectionExtensions
{
    public static IServiceCollection AddFriendFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IFriendService, FriendService>();
        services.AddScoped<IChatService, ChatService>();
        return services;
    }
}
