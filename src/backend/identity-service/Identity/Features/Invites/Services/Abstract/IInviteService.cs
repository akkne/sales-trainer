using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Invites.Models;

namespace Sellevate.Identity.Features.Invites.Services.Abstract;

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
