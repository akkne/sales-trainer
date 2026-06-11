namespace SalesTrainer.Api.Features.Discuss.Models;

/// <summary>
/// Identifies what a <see cref="DiscussVote"/> points at. Votes are upvote-only,
/// so the existence of a row means the user has upvoted that target.
/// </summary>
public enum DiscussVoteTarget
{
    Thread = 0,
    Reply = 1
}
