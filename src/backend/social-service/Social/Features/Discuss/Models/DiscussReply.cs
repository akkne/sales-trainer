namespace Sellevate.Social.Features.Discuss.Models;

public sealed class DiscussReply
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = null!;

    public int UpvoteCount { get; set; }

    public bool IsAccepted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DiscussThread Thread { get; set; } = null!;
}
