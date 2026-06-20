using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Techniques.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class TechniqueSkillEntityConfiguration : IEntityTypeConfiguration<TechniqueSkill>
{
    public void Configure(EntityTypeBuilder<TechniqueSkill> builder)
    {
        builder.ToTable("TechniqueSkills");

        builder.HasKey(link => new { link.TechniqueId, link.SkillId });

        builder.HasIndex(link => link.SkillId);
    }
}
