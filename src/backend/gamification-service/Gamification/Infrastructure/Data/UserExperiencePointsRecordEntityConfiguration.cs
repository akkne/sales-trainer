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
        builder.HasIndex(record => new { record.OrganizationId, record.UserId });

        // Deliberately left global. SourceEventId is a Kafka event id, unique across the whole
        // platform by construction, so this index enforces "one grant per event" — a statement
        // about the event stream, not about an organization. Adding the organization would let the
        // same event be granted once per tenant, which is the opposite of what it is for.
        builder.HasIndex(record => record.SourceEventId)
            .IsUnique()
            .HasFilter("\"SourceEventId\" IS NOT NULL");
    }
}
