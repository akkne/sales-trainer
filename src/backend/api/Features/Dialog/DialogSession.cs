using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SalesTrainer.Api.Features.Dialog;

public class DialogSession
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

public enum DialogSessionStatus
{
    Active,
    Completed,
    Abandoned
}

public class DialogMessage
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

public class DialogFeedback
{
    [BsonElement("content")]
    public string Content { get; set; } = null!;

    [BsonElement("generatedAt")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
