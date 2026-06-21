using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.Gamification.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

public sealed class UserExperiencePointsRecordEntityConfiguration : IEntityTypeConfiguration<UserExperiencePointsRecord>
{
    public void Configure(EntityTypeBuilder<UserExperiencePointsRecord> builder)
    {
        builder.ToTable("UserXpRecords");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Source).IsRequired();
        builder.Property(record => record.SourceEventId).IsRequired(false);
        builder.HasIndex(record => record.UserId);
        builder.HasIndex(record => record.SourceEventId)
            .IsUnique()
            .HasFilter("\"SourceEventId\" IS NOT NULL");
    }
}
