using System.Text.Json;

namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.21. A full replacement of an assignment's editable fields.
///
/// <para>
/// <b>Which fields are still editable depends on the status, and the database has the last word.</b>
/// On a draft, everything here applies. On an active assignment the freeze trigger refuses
/// <c>SourceType</c>, <c>SourceRef</c>, <c>Content</c> and <c>CompletionRule</c> — those are what
/// every recorded attempt was scored against, and changing them retroactively makes every stored
/// score describe something that no longer exists, which is the defect 40.15 and 40.16 spent two
/// blocks removing. The service refuses them first, with a message; the trigger is there because
/// "the service currently refuses" is not the same guarantee as "it cannot be written".
/// </para>
///
/// <para>
/// Title, goal, audience, opening time, deadline and repeat schedule stay editable at every status
/// short of closed: adding three people to a running assignment and extending a deadline are ordinary
/// acts of running a team, not corruption of a record.
/// </para>
/// </summary>
public sealed record UpdateAssignmentRequestDto(
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
