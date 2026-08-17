using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Content.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class OrganizationProfileReplicaEntityConfiguration : IEntityTypeConfiguration<OrganizationProfileReplica>
{
    public void Configure(EntityTypeBuilder<OrganizationProfileReplica> builder)
    {
        builder.ToTable("OrganizationProfileReplicas");

        // The tenant column is the primary key: one profile per organization, and there is no
        // surrogate id to let a second row for the same tenant exist by accident.
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
