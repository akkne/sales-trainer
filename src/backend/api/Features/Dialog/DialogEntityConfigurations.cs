using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Dialog.Models;

namespace SalesTrainer.Api.Features.Dialog;

public class DialogBundleConfiguration : IEntityTypeConfiguration<DialogBundle>
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

public class DialogModeConfiguration : IEntityTypeConfiguration<DialogMode>
{
    public void Configure(EntityTypeBuilder<DialogMode> builder)
    {
        builder.ToTable("DialogModes");

        builder.HasKey(mode => mode.Id);

        builder.Property(mode => mode.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(mode => mode.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(mode => mode.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(mode => mode.ChatSystemPrompt)
            .IsRequired();

        builder.Property(mode => mode.FeedbackSystemPrompt)
            .IsRequired();

        builder.HasOne(mode => mode.Bundle)
            .WithMany(bundle => bundle.Modes)
            .HasForeignKey(mode => mode.BundleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(mode => mode.BundleId);
        builder.HasIndex(mode => new { mode.BundleId, mode.Key }).IsUnique();
    }
}
