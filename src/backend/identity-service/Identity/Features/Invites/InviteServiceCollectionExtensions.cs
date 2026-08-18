using Sellevate.Identity.Features.Invites.Services.Abstract;
using Sellevate.Identity.Features.Invites.Services.Implementation;
using Sellevate.Identity.Infrastructure.Configuration;

namespace Sellevate.Identity.Features.Invites;

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
