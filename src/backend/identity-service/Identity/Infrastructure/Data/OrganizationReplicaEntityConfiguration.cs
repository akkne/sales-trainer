using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Organizations.Models;

namespace Sellevate.Identity.Infrastructure.Data;

/// <summary>
/// Maps the read-only projection of organization-service's tenant registry. The organization identifier
/// is the primary key and is never database-generated — it arrives over Kafka already assigned by the
/// service that owns it.
/// </summary>
public sealed class OrganizationReplicaEntityConfiguration : IEntityTypeConfiguration<OrganizationReplica>
{
    private const int NameMaximumLength = 200;
    private const int SlugMaximumLength = 100;

    public void Configure(EntityTypeBuilder<OrganizationReplica> builder)
    {
        builder.ToTable("OrganizationReplicas");

        builder.HasKey(replica => replica.OrganizationId);
        builder.Property(replica => replica.OrganizationId)
            .ValueGeneratedNever();

        builder.Property(replica => replica.Name)
            .IsRequired()
            .HasMaxLength(NameMaximumLength);

        builder.Property(replica => replica.Slug)
            .IsRequired()
            .HasMaxLength(SlugMaximumLength);

        builder.Property(replica => replica.Status)
            .HasConversion<int>();

        builder.Property(replica => replica.UpdatedAt)
            .IsRequired();
    }
}
