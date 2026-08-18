using Sellevate.Identity.Features.Auth.Constants;

namespace Sellevate.Identity.Features.Auth.Models;

/// <summary>
/// Step 2 of the three-step login flow: what an email address resolved to. Internal to the
/// service — only <see cref="Method"/> ever reaches the client, never
/// <see cref="OrganizationId"/>, which would turn the first login step into an oracle telling an
/// outsider which addresses belong to a customer.
/// </summary>
/// <param name="Method">One of <see cref="AuthMethodNames"/>.</param>
/// <param name="OrganizationId">The organization the address resolved to, or <see langword="null"/>
/// when none matched. Used for logging and for the provider dispatch decision only.</param>
public sealed record ResolvedLoginMethod(string Method, Guid? OrganizationId)
{
    /// <summary>
    /// What an address that matches no organization resolves to. It is a real answer, not an
    /// error: the platform's own accounts (superadmins, users who predate organizations) sign in
    /// with a password, and answering identically for "unknown address" and "known address in an
    /// organization that uses passwords" is what keeps the first step non-enumerable.
    /// </summary>
    public static ResolvedLoginMethod PlatformDefault { get; } = new(AuthMethodNames.Password, null);
}
