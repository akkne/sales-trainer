using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.SkillTree.Models;

namespace SalesTrainer.Api.Infrastructure.Data;

public class SkillStageConfiguration : IEntityTypeConfiguration<SkillStage>
{
    public void Configure(EntityTypeBuilder<SkillStage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(60);
        builder.Property(x => x.Accent).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Order).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
    }
}
