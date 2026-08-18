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

    Task<IssuedTokenPair> AcceptAsync(
        string rawToken,
        AcceptInviteRequestDto request,
        CancellationToken cancellationToken = default);
}
