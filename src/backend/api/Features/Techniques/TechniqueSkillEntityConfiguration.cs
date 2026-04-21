using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Techniques.Models;

namespace SalesTrainer.Api.Features.Techniques;

public sealed class TechniqueSkillEntityConfiguration : IEntityTypeConfiguration<TechniqueSkill>
{
    public void Configure(EntityTypeBuilder<TechniqueSkill> builder)
    {
        builder.ToTable("TechniqueSkills");

        builder.HasKey(link => new { link.TechniqueId, link.SkillId });

        builder.HasIndex(link => link.SkillId);
    }
}
