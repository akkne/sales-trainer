namespace Sellevate.Identity.Features.Admin.Constants;

/// <summary>
/// Wire values of <c>AdminUserDto.AuthProvider</c>. These are display labels for the platform admin
/// screens, not the <c>OrganizationAuthConfiguration.Method</c> domain in
/// <c>AuthMethodNames</c> — a user row records how that individual signs in, which today is decided
/// by whether a Google identity was ever linked to it.
/// </summary>
public static class AuthProviderLabels
{
    public const string Google = "Google";
    public const string Password = "Password";
}
