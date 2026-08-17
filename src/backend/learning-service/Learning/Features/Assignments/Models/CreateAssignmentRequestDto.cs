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
    JsonElement? RepeatSchedule);
