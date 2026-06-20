namespace Sellevate.Social.Features.Discuss.Models;

public sealed class DiscussVote
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DiscussVoteTarget TargetType { get; set; }
    public Guid TargetId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
