using Sellevate.Social.Features.Chat.Services.Abstract;
using Sellevate.Social.Features.Chat.Services.Implementation;
using Sellevate.Social.Features.Friends.Services.Abstract;
using Sellevate.Social.Features.Friends.Services.Implementation;

namespace Sellevate.Social.Features.Friends;

/// <summary>
/// Registers friends and chat, which ship together because chat's precondition is a friendship.
///
/// <para>
/// Phase 40.13. Every registration here is scoped, and the chat repository especially: it takes
/// <c>ITenantContext</c>, which is scoped, so registering it as a singleton would capture the first
/// request's organization and hand it to every later one — the Mongo equivalent of the pooled-DbContext
/// mistake <c>scripts/tenancy-pool-lint.py</c> bans.
/// </para>
/// </summary>
public static class FriendFeatureServiceCollectionExtensions
{
    public static IServiceCollection AddFriendFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IFriendService, FriendService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IChatConversationRepository, ChatConversationRepository>();
        return services;
    }
}
