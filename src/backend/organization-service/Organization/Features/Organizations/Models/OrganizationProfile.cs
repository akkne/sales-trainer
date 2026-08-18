using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>
/// The per-organization content-substitution profile from docs/TENANCY/CONTENT_MODEL.md §3
/// (product, ICP, objections, script, tone, glossary, banned claims). Unlike <see cref="Organization"/>,
/// this row belongs to exactly one tenant and is never shared, so it implements
/// <see cref="ITenantScoped"/> and is protected by the write guard and RLS from Stage A — see
/// docs/DECISIONS.md for why this table, and not the registry, is the tenant-scoped one.
///
/// <para>
/// <see cref="OrganizationId"/> is both the primary key and the tenant column: this is a 1:1
/// row per organization, addressed only through <c>ITenantContext</c>, never through a caller-
/// supplied id (docs/TENANCY/TENANCY.md §1.3).
/// </para>
/// </summary>
public sealed class OrganizationProfile : ITenantScoped
{
    public Guid OrganizationId { get; set; }

    public string? Product { get; set; }

    public string? Icp { get; set; }

    public string ObjectionsJson { get; set; } = "[]";

    public string ScriptJson { get; set; } = "[]";

    public string? Tone { get; set; }

    public string GlossaryJson { get; set; } = "{}";

    public string BannedClaimsJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
