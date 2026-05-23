using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.SkillTree.Models;

namespace SalesTrainer.Api.Infrastructure.Data;

public class SkillEntityConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.IconicName).IsRequired();
        builder.Property(s => s.Title).IsRequired();
        builder.Property(s => s.Stage).IsRequired().HasDefaultValue("general");

        builder.HasIndex(s => s.IconicName).IsUnique();
        builder.HasIndex(s => s.Stage);
    }
}

public class TopicEntityConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("Topics");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.IconicName).IsRequired();
        builder.Property(t => t.Title).IsRequired();

        builder.HasOne(t => t.Skill)
            .WithMany()
            .HasForeignKey(t => t.SkillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.IconicName).IsUnique();
        builder.HasIndex(t => new { t.SkillId, t.OrderInSkill });
    }
}
