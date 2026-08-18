namespace Sellevate.Identity.Infrastructure.Configuration;

/// <summary>
/// How long a platform superadministrator's impersonation session lasts.
/// </summary>
public sealed class ImpersonationConfiguration
{
    public const string SectionName = "Impersonation";

    /// <summary>
    /// Lifetime of an impersonation access token. Deliberately shorter than
    /// <c>Jwt:AccessTokenLifetimeMinutes</c>'s effective session (which is renewable through a
    /// refresh token) because an impersonation token has no refresh companion at all: this value
    /// is the entire session, and extending it means asking again and writing another audit row.
    /// </summary>
    public int TokenLifetimeMinutes { get; set; } = 15;
}
