namespace Sellevate.BuildingBlocks.Eventing;

/// <summary>
/// The single source of truth for Kafka topic names, mirroring the event catalogue
/// in <c>docs/MICROSERVICES.md §4.1</c>. Convention: <c>&lt;aggregate&gt;.&lt;event&gt;</c>,
/// partition key = <c>userId</c> (per-user ordering), at-least-once delivery
/// (consumers dedupe on <see cref="EventEnvelope.EventId"/>).
///
/// <para>
/// Producers/consumers must reference these constants rather than string literals so a
/// rename is a single edit and the compiler catches typos across services.
/// </para>
/// </summary>
public static class Topics
{
    // ── Identity (produces) ────────────────────────────────────────────────
    public const string UserRegistered = "user.registered";
    public const string UserUpdated = "user.updated";
    public const string UserDeleted = "user.deleted";
    public const string UserAvatarChanged = "user.avatar.changed";

    // ── Learning (produces) ────────────────────────────────────────────────
    public const string ExerciseCompleted = "exercise.completed";
    public const string LessonCompleted = "lesson.completed";
    public const string SkillCompleted = "skill.completed";

    // ── AI Engine (produces) ───────────────────────────────────────────────
    public const string DialogEvaluated = "dialog.evaluated";

    // ── Gamification (produces) ────────────────────────────────────────────
    public const string XpGranted = "xp.granted";
    public const string AchievementUnlocked = "achievement.unlocked";
    public const string StreakMilestone = "streak.milestone";
    public const string GamificationDialogWeightsUpdated = "gamification.dialog-weights.updated";

    // ── Social (produces) ──────────────────────────────────────────────────
    public const string FriendRequestReceived = "friend.request.received";
    public const string FriendRequestAccepted = "friend.request.accepted";
    public const string ChatMessageSent = "chat.message.sent";
    public const string ChatMessageRead = "chat.message.read";
    public const string DiscussReplyCreated = "discuss.reply.created";

    // ── Company (produces) ─────────────────────────────────────────────────
    public const string CompanyFollowUpDue = "company.followup.due";

    /// <summary>
    /// Suffix appended to a source topic to form its dead-letter topic. A message that
    /// still fails after the configured retries is published to <c>&lt;topic&gt;.dlt</c>
    /// so it can be inspected/replayed without blocking the partition.
    /// </summary>
    public const string DeadLetterSuffix = ".dlt";

    /// <summary>Builds the dead-letter topic name for <paramref name="sourceTopic"/>.</summary>
    public static string DeadLetterFor(string sourceTopic) => sourceTopic + DeadLetterSuffix;

    /// <summary>
    /// All declared base topic names (excludes the <see cref="DeadLetterSuffix"/> helper and any
    /// dead-letter companions). Reflected once from the <c>const string</c> fields so a newly
    /// added topic is automatically picked up by the startup provisioner.
    /// </summary>
    public static IReadOnlyCollection<string> All { get; } = typeof(Topics)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        // Skip the ".dlt" suffix constant; real topic names never start with a dot.
        .Where(value => !value.StartsWith('.'))
        .Distinct()
        .ToArray();
}
