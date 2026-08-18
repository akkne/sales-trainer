using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;

namespace Sellevate.Learning.Features.Programs.Models;

/// <summary>
/// Phase 40.17. One organization's training programme, frozen at a point in time
/// (docs/TENANCY/CONTENT_MODEL.md §2.5).
///
/// <para>
/// A programme version owns no content whatsoever. It is a numbered container for
/// <see cref="ProgramItem"/> rows, and every one of those is a reference: a skill id, an order, and
/// a pinned lesson version. Rearranging a curriculum therefore writes a new programme version and
/// touches not one lesson and not one lesson version — which is the property CONTENT_MODEL.md §1
/// calls "programme structure is an ordered list of references", and the reason this table can exist
/// at all without reintroducing the per-customer content fork §1 forbids.
/// </para>
///
/// <para>
/// Strict tenant data, not content: unlike <c>Lessons</c> there is no such thing as a global
/// programme. A curriculum is a decision one organization made about its own people, so
/// <see cref="OrganizationId"/> is non-null and the row-level-security policy is plain equality.
/// </para>
/// </summary>
public sealed class ProgramVersion : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Owning organization; never null. The security boundary is the Postgres row-level-security
    /// policy created by the AddProgramVersioning migration — the EF query filter on this property
    /// is convenience (docs/TENANCY/TENANCY.md §1.4–1.5).
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Monotonic per organization, starting at 1. Unique together with <see cref="OrganizationId"/>.</summary>
    public int VersionNumber { get; set; }

    /// <summary>One of <see cref="ProgramVersionStatuses"/>.</summary>
    public string Status { get; set; } = ProgramVersionStatuses.Draft;

    /// <summary>The administrator who opened the draft. Null only for rows created by a background path.</summary>
    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public ICollection<ProgramItem> Items { get; set; } = [];
}
