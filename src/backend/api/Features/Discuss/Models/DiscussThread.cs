namespace SalesTrainer.Api.Features.Discuss.Models;

/// <summary>
/// A community forum topic. Authored by a user, may be marked solved by accepting
/// a reply, and can be pinned or flagged "hot" by an admin.
/// </summary>
public sealed class DiscussThread
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;

    // Denormalized counters kept in sync inside the service for cheap sorting/listing.
    public int UpvoteCount { get; set; }
    public int ReplyCount { get; set; }
    public int ViewCount { get; set; }

    /// <summary>Set => the thread is "solved" (Решено). Points at a <see cref="DiscussReply"/>.</summary>
    public Guid? AcceptedReplyId { get; set; }

    public bool IsPinned { get; set; }
    public bool IsHot { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Bumped when a reply is posted; drives the "new" sort.</summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    public ICollection<DiscussReply> Replies { get; set; } = [];
    public ICollection<DiscussThreadTag> ThreadTags { get; set; } = [];
}
