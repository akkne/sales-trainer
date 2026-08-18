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
    public const string UserRegistered = "user.registered";
    public const string UserUpdated = "user.updated";
    public const string UserDeleted = "user.deleted";
    public const string UserAvatarChanged = "user.avatar.changed";

    public const string ExerciseCompleted = "exercise.completed";
    public const string LessonCompleted = "lesson.completed";
    public const string SkillCompleted = "skill.completed";

    /// <summary>
    /// Phase 40.23. An assignment was issued to one named person — one event per resolved
    /// recipient, published in the same transaction that writes their progress row, so "this
    /// person was asked" and "this person was told" cannot diverge.
    /// </summary>
    public const string AssignmentIssued = "assignment.issued";

    /// <summary>
    /// Phase 40.23. The deadline of an assignment this person has not finished is close. Published
    /// once per assignment per recipient by <c>AssignmentDeadlineNoticeService</c>; extending the
    /// deadline re-arms it, because the notice names a date.
    /// </summary>
    public const string AssignmentDeadlineApproaching = "assignment.deadline.approaching";

    /// <summary>
    /// Phase 40.23. A human — the РОП — asked this person to get on with it. Distinct from
    /// <see cref="AssignmentDeadlineApproaching"/> on purpose: one is a clock, the other is a
    /// person, and collapsing them would make the second one ignorable.
    /// </summary>
    public const string AssignmentReminder = "assignment.reminder";

    /// <summary>
    /// Phase 40.26. A day before an assignment's deadline, somebody on the team has still not opened
    /// it — addressed to the organization's administrators rather than to the person who owes the
    /// work (docs/TENANCY/ASSIGNMENTS.md §5).
    ///
    /// <para>
    /// Separate from <see cref="AssignmentDeadlineApproaching"/> although the same sweep publishes
    /// both in the same transaction: that one is «сделай», this one is «дожми», and the roadmap's
    /// claim is that adoption turns on the second. One topic carrying a recipient role would have
    /// made the digest's dedupe key, its body and its action indistinguishable from the notice it is
    /// about.
    /// </para>
    /// </summary>
    public const string AssignmentDeadlineDigest = "assignment.deadline.digest";

    /// <summary>
    /// Phase 40.25. One person's standing on one assignment moved between the four funnel states
    /// (docs/TENANCY/ASSIGNMENTS.md §4). Published by the threshold evaluator in the transaction that
    /// writes the row, so a transition cannot exist without its notice.
    ///
    /// <para>
    /// <b>The per-organization funnel is not built from this topic and must not be.</b> The РОП's
    /// screen counts progress rows in learning-db, where the rows are; this event exists so
    /// analytics-service can keep a platform-wide operational counter without holding attempts,
    /// scores or a customer id — see docs/ANALYTICS_SERVICE.md.
    /// </para>
    /// </summary>
    public const string AssignmentProgressChanged = "assignment.progress.changed";

    /// <summary>
    /// Phase 40.25. The РОП selected a fragment of this person's practice conversation and commented
    /// on it (docs/TENANCY/ASSIGNMENTS.md §4.1). Addressed to the manager whose conversation it was.
    /// </summary>
    public const string DialogReviewCommented = "dialog.review.commented";

    /// <summary>
    /// Phase 40.25. The РОП has ruled on a disputed AI score. Addressed to the manager who filed it,
    /// whether it was upheld or rejected: a dispute closed in silence is the black box the mechanism
    /// exists to open.
    /// </summary>
    public const string DialogReviewResolved = "dialog.review.resolved";

    /// <summary>
    /// Phase 40.26. A manager has disputed an AI score and it is waiting for a verdict. Addressed to
    /// the organization's administrators — the half of the two-way loop 40.25 built the queue for and
    /// could not push, because nothing in the platform could enumerate an organization's
    /// administrators until this block widened <c>GET /internal/memberships/active</c>.
    /// </summary>
    public const string DialogReviewDisputed = "dialog.review.disputed";

    public const string DialogEvaluated = "dialog.evaluated";

    public const string XpGranted = "xp.granted";
    public const string AchievementUnlocked = "achievement.unlocked";
    public const string StreakMilestone = "streak.milestone";
    public const string GamificationDialogWeightsUpdated = "gamification.dialog-weights.updated";

    public const string FriendRequestReceived = "friend.request.received";
    public const string FriendRequestAccepted = "friend.request.accepted";
    public const string ChatMessageSent = "chat.message.sent";
    public const string ChatMessageRead = "chat.message.read";
    public const string DiscussReplyCreated = "discuss.reply.created";

    public const string CompanyFollowUpDue = "company.followup.due";

    public const string OrganizationCreated = "organization.created";
    public const string OrganizationUpdated = "organization.updated";
    public const string OrganizationSuspended = "organization.suspended";

    /// <summary>
    /// The content-substitution profile changed (Phase 40.19). Separate from
    /// <see cref="OrganizationUpdated"/> on purpose: that event describes the tenant registry and
    /// carries no organization in its envelope, while this one is <em>inside</em> a tenant and its
    /// consumers are ordinary tenant-scoped projections.
    /// </summary>
    public const string OrganizationProfileUpdated = "organization.profile.updated";

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
    ///
    /// <para>
    /// The leading-dot filter is what excludes <see cref="DeadLetterSuffix"/> itself: a real topic
    /// name never starts with a dot, so any future suffix constant declared here is skipped for
    /// free rather than having to be listed by name.
    /// </para>
    /// </summary>
    public static IReadOnlyCollection<string> All { get; } = typeof(Topics)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .Where(value => !value.StartsWith('.'))
        .Distinct()
        .ToArray();
}
