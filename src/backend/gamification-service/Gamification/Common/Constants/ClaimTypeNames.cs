namespace Sellevate.Gamification.Common.Constants;

/// <summary>
/// JWT claim types this service reads off an incoming principal. Kept separate from
/// <see cref="AuthorizationPolicies"/>, which is copied verbatim into every service and must stay
/// identical there.
/// </summary>
public static class ClaimTypeNames
{
    /// <summary>
    /// The raw JWT subject claim. Read as a fallback after <c>ClaimTypes.NameIdentifier</c>, because
    /// a principal built by the JWT handler maps <c>sub</c> to the .NET name-identifier URI while a
    /// principal forwarded by the gateway keeps the wire spelling.
    /// </summary>
    public const string Subject = "sub";
}
