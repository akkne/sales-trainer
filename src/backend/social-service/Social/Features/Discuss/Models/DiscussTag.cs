namespace Sellevate.Social.Features.Discuss.Models;

public sealed class DiscussTag
{
    public Guid Id { get; set; }

    public string Slug { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsCurated { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DiscussThreadTag> ThreadTags { get; set; } = [];
}
