namespace SalesTrainer.Api.Features.Discuss.Models;

/// <summary>
/// A forum category tag. Curated tags are managed by admins; free-form tags are
/// created on the fly when a user types a new tag while creating a thread.
/// </summary>
public sealed class DiscussTag
{
    public Guid Id { get; set; }

    /// <summary>Normalized lowercase identifier, unique. Used to dedupe free-form tags.</summary>
    public string Slug { get; set; } = null!;

    /// <summary>Display label.</summary>
    public string Name { get; set; } = null!;

    /// <summary>True = admin catalog tag; false = user free-form tag.</summary>
    public bool IsCurated { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DiscussThreadTag> ThreadTags { get; set; } = [];
}
