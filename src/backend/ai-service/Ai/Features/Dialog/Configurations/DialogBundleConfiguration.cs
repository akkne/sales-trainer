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

        // Phase 40.11. Every bundle read is "mine or global", so the organization leads the index.
        // Built by docs/TENANCY/sql/40.11_ai_organization_indexes_concurrently.sql, not by the
        // migration — see the note on 20260815_AddOrganizationId.
        builder.HasIndex(bundle => new { bundle.OrganizationId, bundle.SortOrder });
    }
}
