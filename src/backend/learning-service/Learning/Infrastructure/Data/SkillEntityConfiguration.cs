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
        builder.Property(skill => skill.OrganizationId);
        builder.Property(skill => skill.IconicName).IsRequired();
        builder.Property(skill => skill.Title).IsRequired();
        builder.Property(skill => skill.Stage).IsRequired().HasDefaultValue("general");

        // Phase 40.10. The slug is unique *per organization*, not globally — otherwise the second
        // customer that wants its own "objections" skill cannot be created at all. Postgres treats
        // NULLs as distinct in a composite unique index, so the global library (OrganizationId IS
        // NULL) needs its own partial unique index to keep two global "objections" skills apart.
        builder.HasIndex(skill => new { skill.OrganizationId, skill.IconicName })
            .IsUnique();
        builder.HasIndex(skill => skill.IconicName)
            .IsUnique()
            .HasFilter("\"OrganizationId\" IS NULL")
            .HasDatabaseName("IX_Skills_IconicName_Global");

        builder.HasIndex(skill => new { skill.OrganizationId, skill.Stage });
    }
}
