using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.SkillTree.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class SkillEntityConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills");
        builder.HasKey(skill => skill.Id);
        builder.Property(skill => skill.IconicName).IsRequired();
        builder.Property(skill => skill.Title).IsRequired();
        builder.Property(skill => skill.Stage).IsRequired().HasDefaultValue("general");

        builder.HasIndex(skill => skill.IconicName).IsUnique();
        builder.HasIndex(skill => skill.Stage);
    }
}
