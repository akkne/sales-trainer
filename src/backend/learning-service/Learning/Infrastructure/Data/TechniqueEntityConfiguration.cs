using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Techniques.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps a technique, which may be a global library row or one organization's override of one.
///
/// <para>
/// Phase 40.18. The parent foreign key is <c>Restrict</c>, not <c>Cascade</c>: a global technique three
/// customers have overridden must not be deletable in one click, and <c>SetNull</c> would silently
/// promote the overrides to standalone techniques, losing the fact that they were ever derived. Same
/// call as <c>Lesson.ParentLessonId</c> in 40.15.
/// </para>
///
/// <para>
/// Phase 40.10. The slug is unique <b>per organization</b>, with a partial unique index over the global
/// rows — same reasoning as <see cref="SkillEntityConfiguration"/>.
/// </para>
/// </summary>
public sealed class TechniqueEntityConfiguration : IEntityTypeConfiguration<Technique>
{
    public void Configure(EntityTypeBuilder<Technique> builder)
    {
        builder.ToTable("Techniques");

        builder.HasKey(technique => technique.Id);

        builder.Property(technique => technique.OrganizationId);

        builder.HasOne<Technique>()
            .WithMany()
            .HasForeignKey(technique => technique.ParentTechniqueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(technique => technique.BaseContentHash)
            .HasMaxLength(64);

        builder.Property(technique => technique.IsArchived)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(technique => technique.ParentTechniqueId);

        builder.Property(technique => technique.Slug)
            .IsRequired()
            .HasMaxLength(120);

        builder.HasIndex(technique => new { technique.OrganizationId, technique.Slug })
            .IsUnique();
        builder.HasIndex(technique => technique.Slug)
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NULL")
            .HasDatabaseName("IX_Techniques_Slug_Global");

        builder.Property(technique => technique.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(technique => technique.Summary)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(technique => technique.Body)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(technique => technique.Tags)
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(technique => technique.PrimarySkillId);

        builder.Property(technique => technique.Difficulty)
            .IsRequired();

        builder.Property(technique => technique.DialogJson)
            .HasColumnType("jsonb");

        builder.Property(technique => technique.CaseJson)
            .HasColumnType("jsonb");

        builder.Property(technique => technique.SortOrder)
            .IsRequired();

        builder.Property(technique => technique.CreatedAt)
            .IsRequired();

        builder.Property(technique => technique.UpdatedAt)
            .IsRequired();

        builder.HasMany(technique => technique.AdditionalSkills)
            .WithOne()
            .HasForeignKey(techniqueSkill => techniqueSkill.TechniqueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(technique => technique.Coach)
            .WithOne()
            .HasForeignKey<TechniqueCoach>(coach => coach.TechniqueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(technique => new { technique.OrganizationId, technique.PrimarySkillId });
        builder.HasIndex(technique => new { technique.OrganizationId, technique.SortOrder });
    }
}
