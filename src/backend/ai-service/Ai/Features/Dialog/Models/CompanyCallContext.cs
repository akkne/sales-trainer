using MongoDB.Bson.Serialization.Attributes;

namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class CompanyCallContext
{
    [BsonElement("companyName")]
    public string CompanyName { get; set; } = null!;

    [BsonElement("companyDescription")]
    public string CompanyDescription { get; set; } = null!;

    [BsonElement("callGoal")]
    public string? CallGoal { get; set; }

    /// <summary>Optional buyer persona the chat model should role-play as. All persona fields are
    /// null when the call has no persona attached (39.14) — the pre-39.14 no-persona behavior of
    /// this whole context type is unaffected either way.</summary>
    [BsonElement("personaName")]
    public string? PersonaName { get; set; }

    [BsonElement("personaPosition")]
    public string? PersonaPosition { get; set; }

    [BsonElement("personaPersonality")]
    public string? PersonaPersonality { get; set; }

    [BsonElement("personaDifficulty")]
    public string? PersonaDifficulty { get; set; }
}
