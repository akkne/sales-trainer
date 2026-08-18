namespace Sellevate.Identity.Features.Auth.Constants;

/// <summary>
/// The wire values of <c>OrganizationAuthConfiguration.Method</c> and of
/// <c>IAuthProvider.Method</c> — the seam Phase 40.8 exists to establish
/// (docs/TENANCY/TENANCY.md §4.5).
///
/// <para>
/// <see cref="Password"/> is the only one with an implementation. <see cref="Oidc"/> and
/// <see cref="Saml"/> are declared so the column has a closed, checkable domain from the first
/// migration, and so that configuring one of them today produces a deliberate "no provider →
/// login refused" rather than a silent fallback to passwords. Implementing them is explicitly
/// out of scope until a customer pays for it.
/// </para>
/// </summary>
public static class AuthMethodNames
{
    public const string Password = "password";
    public const string Oidc = "oidc";
    public const string Saml = "saml";

    public static readonly IReadOnlyList<string> All = [Password, Oidc, Saml];
}
