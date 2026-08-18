namespace Sellevate.Identity.Features.Invites.Models;

public sealed record CreateInvitesResponseDto(
    IReadOnlyList<CreatedInviteDto> Created,
    IReadOnlyList<RejectedInviteDto> Rejected);
