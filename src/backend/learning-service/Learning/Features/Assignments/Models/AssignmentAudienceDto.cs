using System.Text.Json.Serialization;

namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.21. The audience rule — who the assignment is for, expressed the way it was chosen rather
/// than as a resolved list of people. See <c>AssignmentAudienceKinds</c> for why learning-service
/// stores the rule and not the names.
/// </summary>
/// <param name="Kind">One of <c>AssignmentAudienceKinds</c>.</param>
/// <param name="UserIds">Required and non-empty when the kind is <c>users</c>, absent otherwise.</param>
/// <param name="GroupId">Required when the kind is <c>group</c>, absent otherwise.</param>
public sealed record AssignmentAudienceDto(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("userIds")] IReadOnlyList<Guid>? UserIds = null,
    [property: JsonPropertyName("groupId")] Guid? GroupId = null);
