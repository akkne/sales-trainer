namespace Sellevate.Identity.Features.Invites.Models;

/// <summary>
/// One address from a bulk request that produced no invite, with the reason. A bulk import is
/// partially successful by design — a single malformed address in a pasted list of forty must not
/// discard the other thirty-nine.
/// </summary>
public sealed record RejectedInviteDto(string Email, string Reason);
