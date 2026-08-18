using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Features.Organizations;

/// <summary>
/// Phase 40.19. A read-only copy of one organization's content-substitution profile, owned by
/// organization-service and projected here by <c>OrganizationProfileConsumer</c>
/// (docs/TENANCY/CONTENT_MODEL.md §3, docs/TENANCY/BACKGROUND_JOBS.md).
///
/// <para>
/// The same table exists in learning-db, for the same reason and with the same columns: both
/// services resolve placeholders on a hot path — a lesson open there, a persona reply here — and
/// neither can afford a synchronous hop into organization-service to do it. The duplication is
/// deliberate; the alternative is a shared database, which is the thing the microservice split
/// exists to prevent.
/// </para>
///
/// <para>
/// Strict tenant data, not content: <c>banned_claims</c> with a null owner would read as "every
/// organization's compliance rules at once", which is both wrong and, in a regulated industry,
/// the exact thing the feature is sold to prevent. Plain-equality RLS, tenant column as the
/// primary key.
/// </para>
/// </summary>
public sealed class OrganizationProfileReplica : ITenantScoped
{
    public Guid OrganizationId { get; set; }

    public string? Product { get; set; }

    public string? Icp { get; set; }

    public string? Tone { get; set; }

    public string ObjectionsJson { get; set; } = "[]";

    public string ScriptJson { get; set; } = "[]";

    public string GlossaryJson { get; set; } = "{}";

    public string BannedClaimsJson { get; set; } = "[]";

    /// <summary>The source row's own <c>UpdatedAt</c>, not the time this replica was written.</summary>
    public DateTime UpdatedAt { get; set; }
}
