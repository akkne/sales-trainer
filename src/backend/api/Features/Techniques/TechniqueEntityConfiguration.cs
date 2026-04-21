using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Techniques.Models;

namespace SalesTrainer.Api.Features.Techniques;

public sealed class TechniqueEntityConfiguration : IEntityTypeConfiguration<Technique>
{
    public void Configure(EntityTypeBuilder<Technique> builder)
    {
        builder.ToTable("Techniques");

        builder.HasKey(technique => technique.Id);

        builder.Property(technique => technique.Slug)
            .IsRequired()
            .HasMaxLength(120);

        builder.HasIndex(technique => technique.Slug)
            .IsUnique();

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

        builder.HasIndex(technique => technique.PrimarySkillId);
        builder.HasIndex(technique => technique.SortOrder);
    }
}
