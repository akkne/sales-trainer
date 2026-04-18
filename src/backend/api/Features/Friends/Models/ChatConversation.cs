using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SalesTrainer.Api.Features.Friends.Models;

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

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
