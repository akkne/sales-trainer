namespace Sellevate.Organization.Features.DemoRequests.Models;

/// <summary>
/// The outcome of <c>POST /admin/demo-requests/{id}/provision</c>. Always <c>200</c> — the failure
/// cases that leave a lead unprovisioned are thrown as exceptions the controller renders as
/// <c>409</c>/<c>503</c>/<c>400</c> instead, so a value of this type only ever describes a lead that
/// now has a working organization and a working invite, whether that happened just now or on an
/// earlier call.
///
/// <para>
/// <see cref="InviteExpiresAt"/> is <see langword="null"/> on an <see cref="AlreadyProvisioned"/>
/// response. That timestamp is never stored on <c>DemoRequest</c> — only <c>identity-service</c>'s
/// <c>Invite</c> row knows it — and re-asking identity-service to answer a call that is defined to have
/// **no side effects** would defeat the point of the fast path. A caller that needs the exact expiry of
/// an already-issued invite has to ask identity-service directly, the same as for any other invite.
/// </para>
/// </summary>
public sealed record DemoRequestProvisioningResultDto(
    Guid DemoRequestId,
    string Status,
    string ProvisioningState,
    ProvisionedOrganizationDto Organization,
    Guid InviteId,
    string InviteEmail,
    DateTime? InviteExpiresAt,
    bool AlreadyProvisioned);
