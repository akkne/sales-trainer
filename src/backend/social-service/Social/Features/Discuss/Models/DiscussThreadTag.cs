namespace Sellevate.Social.Features.Discuss.Models;

public sealed class DiscussThreadTag
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public Guid TagId { get; set; }

    public DiscussThread Thread { get; set; } = null!;
    public DiscussTag Tag { get; set; } = null!;
}
