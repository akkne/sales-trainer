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

        // Phase 40.18. Restrict, not cascade: a global mode three customers have overridden must
        // not be deletable in one click, and SetNull would silently promote the overrides to
        // standalone modes, losing the fact that they were ever derived.
        builder.HasOne<DialogMode>()
            .WithMany()
            .HasForeignKey(mode => mode.ParentModeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(mode => mode.BaseContentHash)
            .HasMaxLength(64);

        builder.HasIndex(mode => mode.ParentModeId);

        builder.HasIndex(mode => mode.BundleId);

        // Phase 40.11. The mode key is unique per organization, not per installation. Postgres
        // treats NULLs in a composite unique index as distinct, so the composite index alone would
        // let the global library grow two rows with the same (BundleId, Key) — hence the second,
        // partial index over exactly the global rows. Same shape as 40.10's Skill.IconicName.
        builder.HasIndex(mode => new { mode.OrganizationId, mode.BundleId, mode.Key })
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NOT NULL");
        builder.HasIndex(mode => new { mode.BundleId, mode.Key })
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NULL")
            .HasDatabaseName("IX_DialogModes_BundleId_Key_Global");
    }
}
