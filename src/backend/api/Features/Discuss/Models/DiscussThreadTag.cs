namespace SalesTrainer.Api.Features.Discuss.Models;

/// <summary>Join row linking a <see cref="DiscussThread"/> to a <see cref="DiscussTag"/>.</summary>
public sealed class DiscussThreadTag
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public Guid TagId { get; set; }

    public DiscussThread Thread { get; set; } = null!;
    public DiscussTag Tag { get; set; } = null!;
}
