using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Programs.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps one version of an organization's curriculum.
///
/// <para>
/// <b>At most one mutable draft per organization, in the database rather than in C#</b> — the same race
/// 40.15 refused to lose for lessons, one level up. Two administrators of the same organization pressing
/// "edit the programme" at the same moment would otherwise produce two curricula with no merge story,
/// and a programme is a list of references where a merge looks deceptively easy and silently reorders
/// somebody's training.
/// </para>
/// </summary>
public sealed class ProgramVersionEntityConfiguration : IEntityTypeConfiguration<ProgramVersion>
{
    public void Configure(EntityTypeBuilder<ProgramVersion> builder)
    {
        builder.ToTable("ProgramVersions");

        builder.HasKey(version => version.Id);

        builder.Property(version => version.OrganizationId).IsRequired();
        builder.Property(version => version.VersionNumber).IsRequired();

        builder.Property(version => version.Status)
            .IsRequired()
            .HasMaxLength(16)
            .HasDefaultValue(ProgramVersionStatuses.Draft);

        builder.Property(version => version.CreatedBy);
        builder.Property(version => version.CreatedAt).IsRequired();
        builder.Property(version => version.PublishedAt);

        builder.HasMany(version => version.Items)
            .WithOne(item => item.ProgramVersion)
            .HasForeignKey(item => item.ProgramVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(version => new { version.OrganizationId, version.VersionNumber })
            .IsUnique();

        builder.HasIndex(version => version.OrganizationId)
            .IsUnique()
            .HasFilter($"\"Status\" = '{ProgramVersionStatuses.Draft}'")
            .HasDatabaseName("IX_ProgramVersions_OrganizationId_Draft");
    }
}
