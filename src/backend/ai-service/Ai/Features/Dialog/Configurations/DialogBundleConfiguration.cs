using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Ai.Features.Dialog.Models;

namespace Sellevate.Ai.Features.Dialog;

public sealed class DialogBundleConfiguration : IEntityTypeConfiguration<DialogBundle>
{
    public void Configure(EntityTypeBuilder<DialogBundle> builder)
    {
        builder.ToTable("DialogBundles");

        builder.HasKey(bundle => bundle.Id);

        builder.Property(bundle => bundle.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(bundle => bundle.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(bundle => bundle.IconEmoji)
            .IsRequired()
            .HasMaxLength(10);

        builder.HasIndex(bundle => bundle.SkillId);
        builder.HasIndex(bundle => bundle.SortOrder);
    }
}
