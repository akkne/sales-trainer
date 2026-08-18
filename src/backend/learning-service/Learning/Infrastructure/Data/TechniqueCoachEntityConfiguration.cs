using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Techniques.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps the coach persona attached to a technique. Unique on the technique — at most one coach per
/// technique — which is why <c>AdminTechniquesController</c> reconciles this row in place rather than
/// deleting and re-inserting it inside one <c>SaveChanges</c>.
/// </summary>
public sealed class TechniqueCoachEntityConfiguration : IEntityTypeConfiguration<TechniqueCoach>
{
    public void Configure(EntityTypeBuilder<TechniqueCoach> builder)
    {
        builder.ToTable("TechniqueCoaches");

        builder.HasKey(coach => coach.Id);

        builder.HasIndex(coach => coach.TechniqueId)
            .IsUnique();

        builder.Property(coach => coach.AvatarSeed)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(coach => coach.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(coach => coach.Role)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(coach => coach.Quote)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(coach => coach.ChallengesJson)
            .HasColumnType("jsonb");
    }
}
