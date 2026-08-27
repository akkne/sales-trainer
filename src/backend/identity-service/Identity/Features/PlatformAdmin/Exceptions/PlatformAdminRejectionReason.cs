namespace Sellevate.Identity.Features.PlatformAdmin.Exceptions;

public enum PlatformAdminRejectionReason
{
    /// <summary>identity-service has never seen this organization. Either the id is wrong, or the
    /// <c>organization.created</c> event has not been consumed yet.</summary>
    OrganizationNotKnown = 0,

    /// <summary>The organization is suspended — it may neither be entered nor staffed.</summary>
    OrganizationSuspended = 1,

    /// <summary>The caller is already inside an impersonation session. Impersonating from an
    /// impersonation token would let one borrowed identity reach another.</summary>
    ImpersonationChainingForbidden = 2
}
