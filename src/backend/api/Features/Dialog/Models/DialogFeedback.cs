using MongoDB.Bson.Serialization.Attributes;

namespace SalesTrainer.Api.Features.Dialog.Models;

public sealed class DialogFeedback
{
    [BsonElement("summary")]
    public string Summary { get; set; } = null!;

    [BsonElement("content")]
    public string Content { get; set; } = null!;

    [BsonElement("generatedAt")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
