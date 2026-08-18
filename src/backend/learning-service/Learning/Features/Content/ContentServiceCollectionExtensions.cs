using Sellevate.Learning.Features.Content.Services.Abstract;
using Sellevate.Learning.Features.Content.Services.Implementation;

namespace Sellevate.Learning.Features.Content;

/// <summary>
/// Registers the copy-on-write content override services (Phase 40.18) and the organization profile
/// provider that parameterizes content with them (Phase 40.19).
/// </summary>
public static class ContentServiceCollectionExtensions
{
    /// <summary>
    /// Both registrations are <c>Scoped</c>, and <see cref="IOrganizationProfileProvider"/> must stay
    /// that way: the memo it holds is a per-request memo, and the profile it reads belongs to
    /// whichever tenant the request does. As a singleton it would serve one organization's profile
    /// to every other organization's content for the lifetime of the process.
    /// </summary>
    public static IServiceCollection AddContentOverrideServices(this IServiceCollection services)
    {
        services.AddScoped<IContentOverrideService, ContentOverrideService>();
        services.AddScoped<IOrganizationProfileProvider, OrganizationProfileProvider>();

        return services;
    }
}
