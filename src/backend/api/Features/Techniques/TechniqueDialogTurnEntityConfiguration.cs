using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Techniques.Models;

namespace SalesTrainer.Api.Features.Techniques;

public sealed class TechniqueDialogTurnEntityConfiguration : IEntityTypeConfiguration<TechniqueDialogTurn>
{
    public void Configure(EntityTypeBuilder<TechniqueDialogTurn> builder)
    {
        builder.ToTable("TechniqueDialogTurns");

        builder.HasKey(turn => turn.Id);

        builder.Property(turn => turn.Side)
            .IsRequired()
            .HasMaxLength(8);

        builder.Property(turn => turn.Text)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(turn => turn.AnnotationsJson)
            .HasColumnType("jsonb");

        builder.HasIndex(turn => new { turn.TechniqueId, turn.OrderIndex });
    }
}
