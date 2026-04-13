using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SalesTrainer.Api.Features.Dialog.Models;

public sealed class DialogSession
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    [BsonElement("bundleId")]
    [BsonRepresentation(BsonType.String)]
    public Guid BundleId { get; set; }

    [BsonElement("modeId")]
    [BsonRepresentation(BsonType.String)]
    public Guid ModeId { get; set; }

    [BsonElement("status")]
    public DialogSessionStatus Status { get; set; } = DialogSessionStatus.Active;

    [BsonElement("messages")]
    public List<DialogMessage> Messages { get; set; } = [];

    [BsonElement("feedback")]
    public DialogFeedback? Feedback { get; set; }

    [BsonElement("xpEarned")]
    public int XpEarned { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("completedAt")]
    public DateTime? CompletedAt { get; set; }
}
