using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Techniques.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class TechniqueEntityConfiguration : IEntityTypeConfiguration<Technique>
{
    public void Configure(EntityTypeBuilder<Technique> builder)
    {
        builder.ToTable("Techniques");

        builder.HasKey(technique => technique.Id);

        builder.Property(technique => technique.OrganizationId);

        builder.Property(technique => technique.Slug)
            .IsRequired()
            .HasMaxLength(120);

        // Phase 40.10, same reasoning as SkillEntityConfiguration.
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
