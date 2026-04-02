using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.SkillTree;

namespace SalesTrainer.Api.Infrastructure.Data;

public class SkillEntityConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> skillBuilder)
    {
        skillBuilder.Property(skill => skill.ApplicableSalesTypes)
            .HasColumnType("text[]");
    }
}
