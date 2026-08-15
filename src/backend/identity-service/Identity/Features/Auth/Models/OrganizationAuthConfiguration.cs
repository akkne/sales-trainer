namespace Sellevate.Identity.Features.Auth.Models;

/// <summary>
/// How one organization's people sign in — the roadmap's <c>organization_auth_config</c>
/// (Phase 40.8, docs/TENANCY/TENANCY.md §4.5). One row per organization, keyed by the
/// organization itself.
///
/// <para>
/// Deliberately **not** <see cref="Sellevate.BuildingBlocks.Tenancy.ITenantScoped"/> and
/// deliberately without a row-level-security policy. Its primary read happens on
/// <c>POST /auth/login/start</c>, before authentication, when there is no token, no
/// <c>X-Organization-Id</c> header and therefore no tenant context at all; answering "which
/// organization owns this email domain" is inherently a cross-tenant question, the same reason
/// the <c>Organizations</c> registry in organization-service is not tenant-scoped either
/// (docs/DECISIONS.md, 2026-08-15). A table whose main access path would have to bypass RLS on
/// every single login should not pretend to have RLS.
/// </para>
///
/// <para>
/// Consequence to respect when a write path is added (40.20): the organization must be taken
/// from <c>ITenantContext</c> explicitly in the query, because neither the query filter nor the
/// database will do it here.
/// </para>
/// </summary>
public sealed class OrganizationAuthConfiguration
{
    /// <summary>Primary key. A bare <c>uuid</c> with no foreign key — organization-service owns
    /// the registry in its own database (DB-per-service).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>One of <see cref="Constants.AuthMethodNames"/>. Always
    /// <c>password</c> at this stage; the column exists so adding a provider is a migration of
    /// data, not of the login flow.</summary>
    public string Method { get; set; } = Constants.AuthMethodNames.Password;

    /// <summary>Provider-specific settings as raw <c>jsonb</c> (issuer, client id, metadata URL,
    /// signing certificate). Null while the method is <c>password</c>, which needs none.</summary>
    public string? ProviderSettings { get; set; }

    /// <summary>Email domains that map to this organization at the first login step. Empty means
    /// the organization is only reachable through an existing membership.</summary>
    public string[] AllowedEmailDomains { get; set; } = [];

    /// <summary>Reserved for SSO: create the membership on first successful sign-in from the
    /// customer's directory. Stored, never read — provisioning stays invite-only until an SSO
    /// provider exists to be provisioned from.</summary>
    public bool IsJustInTimeProvisioningEnabled { get; set; }

    /// <summary>Per-organization session lifetime override. Null means the platform default from
    /// <c>Jwt:RefreshTokenLifetimeDays</c>. Stored, not yet applied to token issuance.</summary>
    public TimeSpan? SessionLifetime { get; set; }

    /// <summary>Reserved for SSO/MFA. Stored, never read at this stage.</summary>
    public bool IsMultiFactorAuthenticationRequired { get; set; }

    public DateTime CreatedAt { get; set; }
}
