namespace Sellevate.Identity.Features.Invites.Services.Abstract;

/// <summary>
/// Issues and verifies the single-use invite token. The token is HMAC-signed and carries the
/// organization it was issued for, so <c>POST /auth/invites/{token}/accept</c> — an anonymous call
/// that by definition has no <c>X-Organization-Id</c> yet — can establish a tenant context from
/// cryptographically verified material instead of from a client-supplied field. See
/// docs/DECISIONS.md (2026-08-15, "Invite token carries its organization").
/// </summary>
public interface IInviteTokenFactory
{
    IssuedInviteToken Issue(Guid organizationId);

    /// <summary>
    /// Verifies the signature and returns the organization the token was issued for.
    /// <see langword="false"/> for anything malformed or tampered with — the caller must not touch
    /// the database in that case.
    /// </summary>
    bool TryReadOrganizationId(string rawToken, out Guid organizationId);

    string ComputeTokenHash(string rawToken);
}
