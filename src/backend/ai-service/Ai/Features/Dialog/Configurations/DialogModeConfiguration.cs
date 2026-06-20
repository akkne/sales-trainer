using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Ai.Features.Dialog.Models;

namespace Sellevate.Ai.Features.Dialog;

public sealed class DialogModeConfiguration : IEntityTypeConfiguration<DialogMode>
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
