using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Dialog.Models;

namespace SalesTrainer.Api.Features.Dialog;

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

        builder.HasOne(bundle => bundle.Skill)
            .WithMany()
            .HasForeignKey(bundle => bundle.SkillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(bundle => bundle.SkillId);
        builder.HasIndex(bundle => bundle.SortOrder);
    }
}
