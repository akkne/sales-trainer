using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SalesTrainer.Api.Features.Friends.Models;

public sealed class ChatMessage
{
    [BsonElement("id")]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("senderId")]
    [BsonRepresentation(BsonType.String)]
    public Guid SenderId { get; set; }

    [BsonElement("content")]
    public string Content { get; set; } = "";

    [BsonElement("sentAt")]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
