using MongoDB.Bson.Serialization.Attributes;

namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class DialogMessage
{
    [BsonElement("role")]
    public string Role { get; set; } = null!;

    [BsonElement("content")]
    public string Content { get; set; } = null!;

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("isStopSignal")]
    public bool IsStopSignal { get; set; }
}
