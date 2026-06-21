using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Sellevate.Social.Features.Chat.Models;

public sealed class ChatConversation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("participantIds")]
    public List<Guid> ParticipantIds { get; set; } = [];

    [BsonElement("messages")]
    public List<ChatMessage> Messages { get; set; } = [];

    [BsonElement("lastMessageAt")]
    public DateTime? LastMessageAt { get; set; }

    /// <summary>Per-participant read watermark, keyed by the user id (as a string). Records the
    /// time each participant last opened the conversation; used to suppress unread-message emails.</summary>
    [BsonElement("lastReadAt")]
    public Dictionary<string, DateTime> LastReadAt { get; set; } = [];

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
