namespace Sellevate.Identity.Features.PlatformAdmin.Constants;

/// <summary>
/// Claims that exist only on an impersonation access token (Phase 40.9). An ordinary token never
/// carries them, so any downstream service — and any log line — can tell the two apart without
/// knowing how the token was obtained.
/// </summary>
public static class ImpersonationClaimNames
{
    /// <summary>Marks the token as issued by the impersonation endpoint. Value is always
    /// <c>true</c>; presence is the signal.</summary>
    public const string IsImpersonation = "imp";

    /// <summary>Id of the <c>ImpersonationAuditEntry</c> row this token was minted for, so a
    /// request made with the token can be tied back to the audit record.</summary>
    public const string ImpersonationId = "imp_id";

    /// <summary>The platform staff member behind the impersonation. Equal to <c>sub</c> today —
    /// the impersonator keeps their own identity and only borrows an organization — but stated
    /// separately so that stays true if <c>sub</c> ever becomes the impersonated user.</summary>
    public const string ActorUserId = "imp_actor";
}
