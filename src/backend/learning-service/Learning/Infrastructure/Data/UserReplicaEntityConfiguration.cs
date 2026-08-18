using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Identity;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps the user replica projected in from identity-service.
///
/// <para>
/// <b>Deliberately platform-global: no organization column and none coming.</b> An identity is a
/// cross-organization fact (docs/TENANCY/TENANCY.md §4.2), so this table carries no tenant filter and the
/// consumer that fills it runs in system mode. The user id is the primary key, so a redelivered
/// registration cannot create a second row for the same person.
/// </para>
/// </summary>
public sealed class UserReplicaEntityConfiguration : IEntityTypeConfiguration<UserReplica>
{
    public void Configure(EntityTypeBuilder<UserReplica> builder)
    {
        builder.ToTable("UserReplicas");
        builder.HasKey(userReplica => userReplica.UserId);
        builder.Property(userReplica => userReplica.Email)
            .IsRequired()
            .HasMaxLength(320);
        builder.Property(userReplica => userReplica.DisplayName)
            .IsRequired()
            .HasMaxLength(200);
    }
}
