namespace SalesTrainer.Api.Features.Discuss.Models;

/// <summary>
/// A single upvote by a user on a thread or reply. Polymorphic via
/// <see cref="TargetType"/> + <see cref="TargetId"/>. A unique index on
/// (UserId, TargetType, TargetId) prevents double-voting.
/// </summary>
public sealed class DiscussVote
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DiscussVoteTarget TargetType { get; set; }
    public Guid TargetId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
