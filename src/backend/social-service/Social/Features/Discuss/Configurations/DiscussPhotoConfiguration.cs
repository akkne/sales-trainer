using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Social.Features.Discuss.Constants;
using Sellevate.Social.Features.Discuss.Models;

namespace Sellevate.Social.Features.Discuss.Configurations;

public sealed class DiscussPhotoConfiguration : IEntityTypeConfiguration<DiscussPhoto>
{
    public void Configure(EntityTypeBuilder<DiscussPhoto> builder)
    {
        builder.ToTable("DiscussPhotos");
        builder.HasKey(photo => photo.Id);

        builder.Property(photo => photo.ObjectKey).IsRequired()
            .HasMaxLength(DiscussPhotoConstants.MaximumObjectKeyLength);
        builder.Property(photo => photo.ContentType).IsRequired()
            .HasMaxLength(DiscussPhotoConstants.MaximumContentTypeLength);

        builder.HasIndex(photo =>
            new { photo.OrganizationId, photo.OwnerType, photo.OwnerId, photo.OrderIndex });
    }
}
