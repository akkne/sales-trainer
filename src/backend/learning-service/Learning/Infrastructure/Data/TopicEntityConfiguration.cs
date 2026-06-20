using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.SkillTree.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class TopicEntityConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("Topics");
        builder.HasKey(topic => topic.Id);
        builder.Property(topic => topic.IconicName).IsRequired();
        builder.Property(topic => topic.Title).IsRequired();

        builder.HasOne(topic => topic.Skill)
            .WithMany()
            .HasForeignKey(topic => topic.SkillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(topic => topic.IconicName).IsUnique();
        builder.HasIndex(topic => new { topic.SkillId, topic.OrderInSkill });
    }
}
