using System.Text.Json;

namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.21. What the РОП sends to create an assignment. It carries no organization and no
/// creator: the first comes from the gateway-validated header via <c>ITenantContext</c>
/// (docs/TENANCY/TENANCY.md §1.3) and the second from the caller's token.
///
/// <para>
/// <see cref="CompletionRule"/> has no default and is not optional — see <c>Assignment</c> for why an
/// assignment that completes on a click must not have a resting place in this API either.
/// </para>
/// </summary>
/// <param name="ContentGenerationJobId">
/// Phase 40.31. The finished pipeline run this assignment hands out.
///
/// <para>
/// <b>When it is present, <see cref="SourceType"/> and <see cref="SourceRef"/> in the body are
/// ignored and derived from the run instead.</b> That is the point: a run started from a measured
/// gap carries <c>skill-gap:&lt;stage&gt;@&lt;date&gt;</c>, and the assignment it produces becomes
/// <c>gap_detected</c> pointing at that measurement — without the client ever being trusted to claim
/// either. A run somebody started by pasting a deck becomes <c>training</c> pointing at the frozen
/// <c>lesson-version:&lt;uuid&gt;</c> it produced, which is what 40.21 said that source type meant
/// and what nothing had yet written.
/// </para>
///
/// <para>
/// <see cref="Content"/> stays the caller's when they send one — an assignment may pair the
/// generated exercises with a dialogue and a reading. Left empty, it defaults to the run's own frozen
/// lesson version, so the ordinary case is one field.
/// </para>
/// </summary>
public sealed record CreateAssignmentRequestDto(
    string Title,
    string? Goal,
    string SourceType,
    string? SourceRef,
    IReadOnlyList<AssignmentContentItemDto>? Content,
    AssignmentAudienceDto? Audience,
    DateTime? OpensAt,
    DateTime? Deadline,
    JsonElement CompletionRule,
    JsonElement? RepeatSchedule,
    Guid? ContentGenerationJobId = null);
