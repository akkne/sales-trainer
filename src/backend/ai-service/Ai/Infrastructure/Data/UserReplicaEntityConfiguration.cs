using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Ai.Identity;

namespace Sellevate.Ai.Infrastructure.Data;

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
