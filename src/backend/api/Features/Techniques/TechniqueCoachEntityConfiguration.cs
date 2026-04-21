using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Techniques.Models;

namespace SalesTrainer.Api.Features.Techniques;

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
