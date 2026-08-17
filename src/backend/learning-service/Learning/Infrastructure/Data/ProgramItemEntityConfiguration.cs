using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Programs.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class ProgramItemEntityConfiguration : IEntityTypeConfiguration<ProgramItem>
{
    public void Configure(EntityTypeBuilder<ProgramItem> builder)
    {
        builder.ToTable("ProgramItems");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.OrganizationId).IsRequired();
        builder.Property(item => item.ProgramVersionId).IsRequired();
        builder.Property(item => item.SkillId).IsRequired();
        builder.Property(item => item.LessonId).IsRequired();
        builder.Property(item => item.LessonVersionId).IsRequired();
        builder.Property(item => item.OrderIndex).IsRequired();

        // One lesson may appear once in a programme. Without this, an admin panel bug could pin the
        // same lesson at two different versions in the same curriculum, and the learner's programme
        // would contain the same material twice with two different correct answers.
        builder.HasIndex(item => new { item.ProgramVersionId, item.LessonId })
            .IsUnique();

        // Tenant-leading index, per the convention every 40.10-40.16 table follows: the query filter
        // and the RLS policy put OrganizationId in front of every predicate. The reading order of a
        // programme is (version, position), which is what this serves.
        builder.HasIndex(item => new { item.OrganizationId, item.ProgramVersionId, item.OrderIndex });

        // "Which programmes pin this snapshot" — the question 40.18's staleness review asks before
        // it offers to discard an override, and the reason a lesson version can never simply be
        // deleted once a curriculum references it.
        builder.HasIndex(item => item.LessonVersionId);
    }
}
