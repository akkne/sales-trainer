using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Features.Auth.Services.Abstract;

/// <summary>
/// Step 2 of the three-step login flow: turn an email address into the organization that owns it
/// and the login method that organization configured.
/// </summary>
public interface IOrganizationAuthConfigurationResolver
{
    /// <summary>
    /// Never throws and never reports "unknown address" — an address that matches nothing
    /// resolves to <see cref="ResolvedLoginMethod.PlatformDefault"/>, which is indistinguishable
    /// from a known address in a password organization.
    /// </summary>
    Task<ResolvedLoginMethod> ResolveForEmailAsync(string email, CancellationToken cancellationToken = default);
}
