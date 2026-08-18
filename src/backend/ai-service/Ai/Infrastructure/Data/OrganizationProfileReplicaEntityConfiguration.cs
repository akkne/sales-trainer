using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Ai.Features.Organizations;

namespace Sellevate.Ai.Infrastructure.Data;

/// <summary>
/// Maps the replicated organization profile. The tenant column is the primary key — there is no global
/// profile — and every jsonb column is NOT NULL with an empty-collection default, so a consumer that
/// received a message missing a field writes an empty list rather than a null the prompt builders would
/// have to guard.
/// </summary>
public sealed class OrganizationProfileReplicaEntityConfiguration : IEntityTypeConfiguration<OrganizationProfileReplica>
{
    public void Configure(EntityTypeBuilder<OrganizationProfileReplica> builder)
    {
        builder.ToTable("OrganizationProfileReplicas");

        builder.HasKey(replica => replica.OrganizationId);

        builder.Property(replica => replica.OrganizationId)
            .ValueGeneratedNever();

        builder.Property(replica => replica.Product).HasColumnType("text");
        builder.Property(replica => replica.Icp).HasColumnType("text");
        builder.Property(replica => replica.Tone).HasColumnType("text");

        builder.Property(replica => replica.ObjectionsJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(replica => replica.ScriptJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(replica => replica.GlossaryJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(replica => replica.BannedClaimsJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(replica => replica.UpdatedAt).IsRequired();
    }
}
