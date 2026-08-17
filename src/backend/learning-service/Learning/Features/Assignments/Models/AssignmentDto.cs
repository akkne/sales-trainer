using System.Text.Json;

namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.21. One assignment as the admin panel reads it.
///
/// <para>
/// <see cref="CompletionRule"/> and <see cref="RepeatSchedule"/> come back as raw JSON rather than as
/// typed records because 40.21 does not own their vocabularies — 40.22 and 40.24 do. Handing back
/// exactly what was stored keeps this block from inventing a shape those blocks would then have to
/// break.
/// </para>
/// </summary>
public sealed record AssignmentDto(
    Guid Id,
    string Title,
    string? Goal,
    string SourceType,
    string? SourceRef,
    IReadOnlyList<AssignmentContentItemDto> Content,
    AssignmentAudienceDto Audience,
    DateTime? OpensAt,
    DateTime? Deadline,
    JsonElement CompletionRule,
    JsonElement? RepeatSchedule,
    string Status,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ActivatedAt,
    DateTime? ClosedAt);
