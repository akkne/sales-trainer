using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Discuss.Models;

namespace SalesTrainer.Api.Features.Discuss.Configurations;

public class DiscussPhotoConfiguration : IEntityTypeConfiguration<DiscussPhoto>
{
    public void Configure(EntityTypeBuilder<DiscussPhoto> builder)
    {
        builder.ToTable("DiscussPhotos");
        builder.HasKey(photo => photo.Id);

        builder.Property(photo => photo.ObjectKey).IsRequired().HasMaxLength(512);
        builder.Property(photo => photo.ContentType).IsRequired().HasMaxLength(100);

        builder.HasIndex(photo => new { photo.OwnerType, photo.OwnerId, photo.OrderIndex });
    }
}
