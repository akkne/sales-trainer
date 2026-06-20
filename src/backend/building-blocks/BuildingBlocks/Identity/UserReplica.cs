namespace Sellevate.BuildingBlocks.Identity;

/// <summary>
/// The small local read-model copy of a user that every non-Identity service keeps,
/// kept in sync via <c>user.*</c> Kafka events. This is how database-per-service is
/// achieved without cross-service joins into Identity's tables: a service stores just
/// the fields it needs to render (name, avatar) and never reaches into Identity's DB.
///
/// <para>
/// Identity owns the source of truth for <see cref="UserId"/>; downstream services own
/// their own copies of this row and treat it as read-only projection state.
/// </para>
/// </summary>
public class UserReplica
{
    /// <summary>The user's id, as issued by Identity. Primary key in every replica table.</summary>
    public Guid UserId { get; set; }

    /// <summary>Display name, refreshed from <c>user.registered</c> / <c>user.updated</c>.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Avatar storage key, refreshed from <c>user.avatar.changed</c>; null if none.</summary>
    public string? AvatarKey { get; set; }

    /// <summary>When this replica row was last updated from an event (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
