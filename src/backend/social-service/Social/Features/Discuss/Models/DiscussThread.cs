namespace Sellevate.Social.Features.Discuss.Models;

public sealed class DiscussThread
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;

    public int UpvoteCount { get; set; }
    public int ReplyCount { get; set; }
    public int ViewCount { get; set; }

    public Guid? AcceptedReplyId { get; set; }

    public bool IsPinned { get; set; }
    public bool IsHot { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    public ICollection<DiscussReply> Replies { get; set; } = [];
    public ICollection<DiscussThreadTag> ThreadTags { get; set; } = [];
}
