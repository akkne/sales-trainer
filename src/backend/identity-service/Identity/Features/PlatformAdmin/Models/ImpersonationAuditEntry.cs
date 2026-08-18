namespace Sellevate.Identity.Features.PlatformAdmin.Models;

/// <summary>
/// One record of a platform superadmin minting a token for someone else's organization
/// (Phase 40.9, docs/TENANCY/TENANCY.md §1.3). Written before the token is handed out, so a token
/// that exists always has a row behind it.
///
/// <para>
/// It lives in identity-db rather than in a separate audit store because identity-service is what
/// mints the token: writing the row and issuing the token in the same database means the audit
/// trail cannot silently fall behind the thing it audits. Rows are append-only — nothing in the
/// codebase updates or deletes them.
/// </para>
///
/// <para>
/// Not <c>ITenantScoped</c>: the whole point of the record is that it crosses tenants, and the
/// people who need to read it are platform staff, not the organization named in it.
/// </para>
/// </summary>
public sealed class ImpersonationAuditEntry
{
    public Guid Id { get; set; }

    /// <summary>The platform superadmin who asked for the token.</summary>
    public Guid ActorUserId { get; set; }

    /// <summary>Copied at write time so the record still reads correctly if the account is later
    /// renamed or removed.</summary>
    public string ActorEmail { get; set; } = string.Empty;

    /// <summary>The organization the token was minted for.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Copied at write time for the same reason as <see cref="ActorEmail"/>.</summary>
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>Free-text justification supplied by the caller. Required — an impersonation with
    /// no stated reason is exactly the one nobody can review afterwards.</summary>
    public string Reason { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; }

    /// <summary>When the issued token stops being accepted. Impersonation tokens are deliberately
    /// short-lived and have no refresh token, so this is the true end of the session.</summary>
    public DateTime ExpiresAt { get; set; }
}
