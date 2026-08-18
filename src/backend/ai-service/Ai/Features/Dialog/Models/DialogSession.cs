using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class DialogSession : ITenantScoped
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Phase 40.11. Owning organization; never empty. Mongo has no row-level security, so unlike
    /// every Postgres table in this codebase there is no database-side net under this field: the
    /// filter is application-level and lives in exactly one place,
    /// <c>DialogSessionRepository</c> (docs/TENANCY/TENANCY.md 1.6, docs/AI_DIALOG.md).
    ///
    /// <para>
    /// It is also the first component of the compound indexes and the designated prefix of any
    /// future shard key, so a scan can never start outside one organization's slice — see
    /// docs/TENANCY/mongo/40.11_dialog_sessions_organization_backfill.js.
    /// </para>
    /// </summary>
    [BsonElement("organizationId")]
    [BsonRepresentation(BsonType.String)]
    public Guid OrganizationId { get; set; }

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

    [BsonElement("companyCallContext")]
    public CompanyCallContext? CompanyCallContext { get; set; }

    [BsonElement("customScenarioContext")]
    public CustomScenarioContext? CustomScenarioContext { get; set; }

    /// <summary>
    /// Phase 40.23. Set when this conversation is a piece of work somebody was assigned. Resolved
    /// from learning-service at session start and frozen there: an assignment edited or closed
    /// mid-conversation must not change the character the person is already talking to.
    /// </summary>
    [BsonElement("assignmentPracticeContext")]
    public AssignmentPracticeContext? AssignmentPracticeContext { get; set; }

    [BsonElement("voiceSeconds")]
    public int VoiceSeconds { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("completedAt")]
    public DateTime? CompletedAt { get; set; }
}
