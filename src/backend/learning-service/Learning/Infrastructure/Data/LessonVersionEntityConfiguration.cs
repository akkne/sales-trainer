using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Lessons.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class LessonVersionEntityConfiguration : IEntityTypeConfiguration<LessonVersion>
{
    public void Configure(EntityTypeBuilder<LessonVersion> builder)
    {
        builder.ToTable("LessonVersions");

        builder.HasKey(version => version.Id);

        builder.Property(version => version.OrganizationId);

        builder.Property(version => version.LessonId).IsRequired();

        builder.Property(version => version.VersionNumber).IsRequired();

        builder.Property(version => version.Content)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(version => version.ContentHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(version => version.Status)
            .IsRequired()
            .HasMaxLength(16)
            .HasDefaultValue(LessonVersionStatuses.Draft);

        builder.Property(version => version.BaseVersionId);
        builder.Property(version => version.IsBreaking).IsRequired();
        builder.Property(version => version.CreatedBy);
        builder.Property(version => version.CreatedAt).IsRequired();
        builder.Property(version => version.PublishedAt);

        builder.HasOne(version => version.Lesson)
            .WithMany()
            .HasForeignKey(version => version.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        // Provenance pointer, not ownership: SetNull rather than Restrict because deleting the
        // global lesson a customer forked from must not be blocked by the fork, and a null base is
        // a state 40.18's staleness review can act on ("unknown base, needs review") while a
        // dangling id is a silent wrong answer.
        builder.HasOne<LessonVersion>()
            .WithMany()
            .HasForeignKey(version => version.BaseVersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(version => new { version.LessonId, version.VersionNumber })
            .IsUnique();

        // The rule from docs/TENANCY/CONTENT_MODEL.md §2.1, enforced by the database rather than by
        // application code: at most one mutable draft per lesson. Two concurrent admins are exactly
        // the case a check-then-insert in C# loses, and two drafts are two branches with no merge
        // story for prose and grading criteria.
        builder.HasIndex(version => version.LessonId)
            .IsUnique()
            .HasFilter($"\"Status\" = '{LessonVersionStatuses.Draft}'")
            .HasDatabaseName("IX_LessonVersions_LessonId_Draft");

        // Tenant-leading index, per the convention every 40.10-40.13 table follows: the query
        // filter and the RLS policy put OrganizationId in front of every predicate.
        builder.HasIndex(version => new { version.OrganizationId, version.LessonId, version.VersionNumber });

        builder.HasIndex(version => version.BaseVersionId);
    }
}
