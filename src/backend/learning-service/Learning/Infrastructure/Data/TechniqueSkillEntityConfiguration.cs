using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Techniques.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps the technique-to-skill link table. The composite key is the uniqueness guarantee — one link per
/// pair — and the extra index on the skill covers the reverse question, "which techniques train this
/// skill".
/// </summary>
public sealed class TechniqueSkillEntityConfiguration : IEntityTypeConfiguration<TechniqueSkill>
{
    public void Configure(EntityTypeBuilder<TechniqueSkill> builder)
    {
        builder.ToTable("TechniqueSkills");

        builder.HasKey(link => new { link.TechniqueId, link.SkillId });

        builder.HasIndex(link => link.SkillId);
    }
}
