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
    public const string TechniqueMasteryChanged = "technique.mastery.changed";

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
}
