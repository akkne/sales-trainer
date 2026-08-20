namespace Sellevate.Organization.Features.DemoRequests.Models;

/// <summary>
/// How far <c>POST /admin/demo-requests/{id}/provision</c> has carried one lead toward a working
/// organization. There is no in-flight "provisioning" member: the call is synchronous end to end, so
/// there is nothing an observer could ever catch mid-transition — a lead is only ever read back
/// between two calls, never during one.
/// </summary>
public enum DemoRequestProvisioningState
{
    /// <summary>Nobody has attempted to provision this lead yet.</summary>
    NotProvisioned,

    /// <summary>
    /// The organization row exists and its creation has been published, but the bootstrap admin
    /// invite has not. A lead parked here is exactly what a failed or interrupted call to
    /// identity-service leaves behind — calling provision again is always safe and converges.
    /// </summary>
    OrganizationCreated,

    /// <summary>The organization exists and its first administrator has a working invite.</summary>
    AdminInvited,
}
