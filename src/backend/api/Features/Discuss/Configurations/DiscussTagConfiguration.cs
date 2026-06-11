using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Discuss.Models;

namespace SalesTrainer.Api.Features.Discuss.Configurations;

public class DiscussTagConfiguration : IEntityTypeConfiguration<DiscussTag>
{
    public void Configure(EntityTypeBuilder<DiscussTag> builder)
    {
        builder.ToTable("DiscussTags");
        builder.HasKey(tag => tag.Id);

        builder.Property(tag => tag.Slug).IsRequired().HasMaxLength(60);
        builder.Property(tag => tag.Name).IsRequired().HasMaxLength(60);

        builder.HasIndex(tag => tag.Slug).IsUnique();
        builder.HasIndex(tag => tag.IsCurated);
    }
}
