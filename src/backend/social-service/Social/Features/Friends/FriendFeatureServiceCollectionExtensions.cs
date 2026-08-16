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

        // Phase 40.13. Scoped, not singleton: it takes ITenantContext, which is scoped. Registering
        // it as a singleton would capture the first request's tenant and hand it to every later one
        // — the Mongo equivalent of the pooled-DbContext mistake scripts/tenancy-pool-lint.py bans.
        services.AddScoped<IChatConversationRepository, ChatConversationRepository>();
        return services;
    }
}
