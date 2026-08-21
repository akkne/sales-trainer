using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Invites.Models;

namespace Sellevate.Identity.Features.Invites.Services.Abstract;

/// <summary>
/// The invite lifecycle. Creating never fails wholesale on a bad address: each requested email is
/// either accepted into <c>Created</c> or explained in <c>Rejected</c>, so one typo in a bulk invite
/// does not discard the rest. Accepting throws <c>InviteNotAcceptableException</c> carrying the reason,
/// which the endpoint maps onto a status code.
/// </summary>
public interface IInviteService
{
    Task<CreateInvitesResponseDto> CreateAsync(
        CreateInvitesRequestDto request,
        Guid invitedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary><see langword="false"/> when the invite does not exist in the caller's
    /// organization — including when it exists in another one.</summary>
    Task<bool> RevokeAsync(Guid inviteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The caller's organization's invites, newest first, without the raw token — it exists only in
    /// the creation response and in the invitee's mailbox.
    /// </summary>
    Task<IReadOnlyList<InviteSummaryDto>> ListAsync(
        InviteStatusFilter statusFilter, CancellationToken cancellationToken = default);

    Task<IssuedTokenPair> AcceptAsync(
        string rawToken,
        AcceptInviteRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only check for whether a token could still be accepted, without accepting it — the
    /// acceptance page uses this before it asks the invitee to fill in a name and password
    /// (docs/AUDIT_PROD.md X-10). Throws the same <see cref="Exceptions.InviteNotAcceptableException"/>
    /// as <see cref="AcceptAsync"/>, except <see cref="Exceptions.InviteRejectionReason.PasswordRequired"/>,
    /// which depends on whether the invitee already has an account and has no bearing on whether the
    /// token itself is still live.
    /// </summary>
    Task ValidateAsync(string rawToken, CancellationToken cancellationToken = default);
}
