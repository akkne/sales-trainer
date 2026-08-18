using Sellevate.Identity.Features.Invites.Services.Abstract;
using Sellevate.Identity.Features.Invites.Services.Implementation;
using Sellevate.Identity.Infrastructure.Configuration;

namespace Sellevate.Identity.Features.Invites;

/// <summary>
/// Registers the invite feature: the token factory that signs and verifies invite tokens, and the
/// service that owns their lifecycle.
/// </summary>
public static class InviteServiceCollectionExtensions
{
    public static IServiceCollection AddInviteFeatureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<InviteConfiguration>(configuration.GetSection(InviteConfiguration.SectionName));
        services.AddScoped<IInviteTokenFactory, InviteTokenFactory>();
        services.AddScoped<IInviteService, InviteService>();
        return services;
    }
}
