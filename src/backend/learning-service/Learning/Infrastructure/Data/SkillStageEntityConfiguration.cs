using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.SkillTree.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class SkillStageEntityConfiguration : IEntityTypeConfiguration<SkillStage>
{
    public void Configure(EntityTypeBuilder<SkillStage> builder)
    {
        builder.HasKey(stage => stage.Id);
        builder.Property(stage => stage.Key).IsRequired().HasMaxLength(40);
        builder.Property(stage => stage.Label).IsRequired().HasMaxLength(60);
        builder.Property(stage => stage.Accent).IsRequired().HasMaxLength(40);
        builder.Property(stage => stage.Order).IsRequired();
        builder.HasIndex(stage => stage.Key).IsUnique();
    }
}
