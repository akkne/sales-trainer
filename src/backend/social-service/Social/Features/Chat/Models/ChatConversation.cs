using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Sellevate.Social.Features.Chat.Models;

public sealed class ChatConversation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Phase 40.13. The organization this conversation belongs to. There is no RLS in Mongo, so
    /// this field is enforced entirely by <c>ChatConversationRepository</c> — every filter in that
    /// class starts from it, and it is also fixed as the mandatory leading component of any future
    /// shard key, the same decision ai-service made for dialog sessions in 40.11.
    ///
    /// <para>
    /// Stored as a string, not a Guid: it matches how <c>userId</c> and <c>senderId</c> are already
    /// stored in this collection, so a mongosh query written by a human uses one convention rather
    /// than two.
    /// </para>
    /// </summary>
    [BsonElement("organizationId")]
    [BsonRepresentation(BsonType.String)]
    public Guid OrganizationId { get; set; }

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
