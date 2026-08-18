namespace Sellevate.Identity.Infrastructure.Configuration;

/// <summary>
/// Invite token signing and lifetime, plus the acceptance URL the invitation email links to.
/// </summary>
public sealed class InviteConfiguration
{
    public const string SectionName = "Invites";

    /// <summary>
    /// HMAC key for the invite token signature. Left empty in local/dev and in tests, where the
    /// already-validated <c>Jwt:Key</c> (guaranteed at least 32 bytes by Program.cs) is used
    /// instead — an invite token is exactly as sensitive as an access token, so reusing that key
    /// avoids a second secret nobody would rotate. See docs/DECISIONS.md (2026-08-15).
    /// </summary>
    public string? SigningKey { get; set; }

    public int TokenLifetimeHours { get; set; } = 168;

    /// <summary>
    /// Base URL of the invite acceptance screen; the raw token is appended as the last path
    /// segment when building the email link.
    /// </summary>
    public string AcceptUrl { get; set; } = "http://localhost:3000/invite";
}
