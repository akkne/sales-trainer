namespace Sellevate.Identity.Common.Constants;

/// <summary>
/// JWT claim types identity-service mints and reads back. These are wire values shared with every
/// other service and with the frontend, so they are a contract: changing a spelling invalidates
/// every token already in circulation.
///
/// <para>
/// The organization-role claim is deliberately absent here — it lives on
/// <see cref="AuthorizationPolicies.OrganizationRoleClaimType"/>, because the policy class is copied
/// verbatim into every service and must stay identical there. Use that one rather than declaring a
/// second constant for the same value.
/// </para>
/// </summary>
public static class ClaimTypeNames
{
    /// <summary>
    /// The raw JWT subject claim. Read as a fallback after <c>ClaimTypes.NameIdentifier</c>, because a
    /// principal built by the JWT handler maps <c>sub</c> to the .NET name-identifier URI while a
    /// principal forwarded by the gateway keeps the wire spelling.
    /// </summary>
    public const string Subject = "sub";

    /// <summary>The user's display name, carried so the frontend can render a session without a
    /// profile round-trip.</summary>
    public const string DisplayName = "displayName";

    /// <summary>
    /// The organization the token is scoped to. Absent when the holder has no active membership —
    /// absence means "no organization", never "every organization".
    /// </summary>
    public const string OrganizationId = "org_id";
}
