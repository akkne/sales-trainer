namespace Sellevate.Identity.Features.Invites.Models;

/// <summary>
/// One successfully created invite. <see cref="Token"/> is the raw single-use token and is
/// returned exactly here, once — the database keeps only its hash, so it can never be shown again.
/// </summary>
public sealed record CreatedInviteDto(
    Guid Id,
    string Email,
    string Role,
    DateTime ExpiresAt,
    string Token);
