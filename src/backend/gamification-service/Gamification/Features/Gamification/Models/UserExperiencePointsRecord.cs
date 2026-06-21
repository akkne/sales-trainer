namespace Sellevate.Gamification.Features.Gamification.Models;

public sealed class UserExperiencePointsRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime EarnedAt { get; set; }

    /// <summary>
    /// The EventId of the integration event that triggered this grant.
    /// Null for direct/non-event grants. Used for DB-level idempotency.
    /// </summary>
    public Guid? SourceEventId { get; set; }
}
