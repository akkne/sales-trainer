namespace Sellevate.Social.Features.Friends.Models;

public sealed class Friendship
{
    public Guid Id { get; set; }
    public Guid RequesterId { get; set; }
    public Guid AddresseeId { get; set; }
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }

    // Computed (stored) columns — populated by the database, never set by application code.
    // They enforce that (A,B) and (B,A) cannot coexist via a unique index on (LowId, HighId).
    public Guid CanonicalLowId { get; private set; }
    public Guid CanonicalHighId { get; private set; }
}
