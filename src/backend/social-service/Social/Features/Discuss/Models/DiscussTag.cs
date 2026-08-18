namespace Sellevate.Social.Features.Discuss.Models;

public sealed class DiscussTag
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.13. <see langword="null"/> means a platform-global tag — the curated vocabulary
    /// every organization shares — and a value means a tag one organization created for itself.
    /// This is the CONTENT flavour of tenancy (docs/TENANCY/TENANCY.md §1.2), the same shape
    /// learning-service's skills and ai-service's dialog modes use, which is why the query filter
    /// for this entity is <c>OrganizationId == null || OrganizationId == current</c> rather than
    /// plain equality.
    ///
    /// <para>
    /// A tag is the one thing in this database that is genuinely shareable: it is a word, not
    /// somebody's content. Threads, replies, votes, photos and friendships all get the strict
    /// flavour instead, because every one of them is a person saying something to their colleagues.
    /// </para>
    /// </summary>
    public Guid? OrganizationId { get; set; }

    public string Slug { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsCurated { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DiscussThreadTag> ThreadTags { get; set; } = [];
}
