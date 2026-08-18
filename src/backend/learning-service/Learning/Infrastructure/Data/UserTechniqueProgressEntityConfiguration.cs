using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Techniques.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps one learner's standing on one technique. Organization first, per docs/TENANCY/TENANCY.md §3.
/// </summary>
public sealed class UserTechniqueProgressEntityConfiguration : IEntityTypeConfiguration<UserTechniqueProgress>
{
    public void Configure(EntityTypeBuilder<UserTechniqueProgress> builder)
    {
        builder.ToTable("UserTechniqueProgress");

        builder.HasKey(progress => progress.Id);

        builder.Property(progress => progress.OrganizationId)
            .IsRequired();

        builder.HasIndex(progress => new { progress.OrganizationId, progress.UserId, progress.TechniqueId })
            .IsUnique();

        builder.HasIndex(progress => new { progress.OrganizationId, progress.UserId });
    }
}
