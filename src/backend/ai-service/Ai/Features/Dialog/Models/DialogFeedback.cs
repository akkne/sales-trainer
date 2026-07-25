using MongoDB.Bson.Serialization.Attributes;

namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class DialogFeedback
{
    [BsonElement("summary")]
    public string Summary { get; set; } = null!;

    [BsonElement("content")]
    public string Content { get; set; } = null!;

    /// <summary>Overall performance grade from 0 to 10 shown to the user.</summary>
    [BsonElement("score")]
    public int Score { get; set; }

    [BsonElement("generatedAt")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
