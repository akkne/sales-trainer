namespace SalesTrainer.Api.Features.Discuss.Models;

public sealed class DiscussPhoto
{
    public Guid Id { get; set; }
    public DiscussPhotoOwner OwnerType { get; set; }
    public Guid OwnerId { get; set; }
    public string ObjectKey { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public int OrderIndex { get; set; }
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
