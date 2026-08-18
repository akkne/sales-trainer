using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Sellevate.Ai.Features.Dialog.Models;

/// <summary>
/// Phase 40.23. The assignment this practice conversation counts towards, and who the AI is playing
/// in it (docs/TENANCY/ASSIGNMENTS.md §6).
///
/// <para>
/// <b>The third context slot on a session, deliberately shaped like the first two.</b>
/// <c>CompanyCallContext</c> and <c>CustomScenarioContext</c> established the seam the roadmap
/// points at: a nullable object stored on the session at start, a static prompt builder that returns
/// the base prompt untouched when it is null, and a place in one chain in <c>DialogService</c>. A
/// fourth mechanism would have meant a fourth thing to remember when 40.19's substitutions or its
/// banned-claims block move.
/// </para>
///
/// <para>
/// <b>It never arrives in a request.</b> Unlike the other two, this one is fetched from
/// learning-service when the session starts, because the person who would have carried it in their
/// request body is the person being graded against it.
/// </para>
/// </summary>
public sealed class AssignmentPracticeContext
{
    [BsonElement("assignmentId")]
    [BsonRepresentation(BsonType.String)]
    public Guid AssignmentId { get; set; }

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("goal")]
    [BsonIgnoreIfNull]
    public string? Goal { get; set; }

    [BsonElement("personaName")]
    [BsonIgnoreIfNull]
    public string? PersonaName { get; set; }

    [BsonElement("personaPosition")]
    [BsonIgnoreIfNull]
    public string? PersonaPosition { get; set; }

    [BsonElement("personaPersonality")]
    [BsonIgnoreIfNull]
    public string? PersonaPersonality { get; set; }

    /// <summary>
    /// <c>Easy</c>, <c>Hard</c>, or anything else for the middle — the same three-way vocabulary
    /// <see cref="Helpers.CompanyContextPromptBuilder"/> renders for a company call.
    /// </summary>
    [BsonElement("personaDifficulty")]
    [BsonIgnoreIfNull]
    public string? PersonaDifficulty { get; set; }
}
