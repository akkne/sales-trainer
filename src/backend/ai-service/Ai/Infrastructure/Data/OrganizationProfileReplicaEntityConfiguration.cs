using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Ai.Features.Organizations;

namespace Sellevate.Ai.Infrastructure.Data;

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
