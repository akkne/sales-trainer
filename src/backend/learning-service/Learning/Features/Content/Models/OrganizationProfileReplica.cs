using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Learning.Features.Content.Models;

/// <summary>
/// Phase 40.19. A read-only copy of one organization's content-substitution profile, owned by
/// organization-service and projected here by <c>OrganizationProfileConsumer</c>
/// (docs/TENANCY/CONTENT_MODEL.md §3, docs/TENANCY/BACKGROUND_JOBS.md).
///
/// <para>
/// <b>Why a replica and not a call to organization-service.</b> Placeholders are resolved on the
/// read path — every lesson open, every exercise render, every graded answer. A synchronous
/// cross-service call there would put organization-service on the critical path of the whole
/// learning product: when it is slow, lessons are slow; when it is down, lessons are down, to
/// deliver a substitution whose absence is merely cosmetic. The same reasoning already produced
/// <c>UserReplicas</c> (40.2) and <c>OrganizationReplicas</c> (40.9), and the cost is the same one
/// those pay — the profile is eventually consistent, so a save takes a moment to reach a lesson.
/// </para>
///
/// <para>
/// Strict tenant data, not content: there is no global profile, and a null owner would mean "every
/// organization's substitutions", so the RLS policy is plain equality. The columns mirror the
/// source table one for one, jsonb included, so that <c>OrganizationProfileSnapshot.FromJson</c>
/// parses the same bytes here as it would at the source.
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

    /// <summary>
    /// The source row's own <c>UpdatedAt</c>, not the time this replica was written. Kafka gives
    /// per-partition ordering and the partition key is the organization id, so events for one
    /// organization already arrive in order; carrying the source timestamp is what lets a reader
    /// tell how stale the copy is without guessing from the projection's own clock.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
