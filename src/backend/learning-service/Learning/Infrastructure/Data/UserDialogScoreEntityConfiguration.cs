using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Assignments.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class UserDialogScoreEntityConfiguration : IEntityTypeConfiguration<UserDialogScore>
{
    public void Configure(EntityTypeBuilder<UserDialogScore> builder)
    {
        builder.ToTable("UserDialogScores");

        builder.HasKey(score => score.Id);

        builder.Property(score => score.OrganizationId).IsRequired();
        builder.Property(score => score.UserId).IsRequired();

        builder.Property(score => score.SessionId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(score => score.DialogModeKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(score => score.DialogModeId).IsRequired();
        builder.Property(score => score.Score).IsRequired();
        builder.Property(score => score.EvaluatedAt).IsRequired();

        // The idempotency guarantee, stated as a constraint rather than as consumer discipline. A
        // redelivered dialog.evaluated - which Kafka promises and the Redis dedupe store only
        // postpones - hits this index and writes nothing, so AttemptCount cannot drift upward while
        // nobody is practising.
        builder.HasIndex(score => new { score.OrganizationId, score.UserId, score.SessionId })
            .IsUnique();

        // The evaluator's query: one person's conversations on one scenario since an assignment was
        // issued. Tenant-leading, per the convention every table since 40.10 follows.
        builder.HasIndex(score => new
        {
            score.OrganizationId,
            score.UserId,
            score.DialogModeKey,
            score.EvaluatedAt,
        });
    }
}
