using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.BuildingBlocks.Outbox;

namespace Sellevate.Identity.Infrastructure.Data;

/// <summary>
/// Maps the outbox table. <c>DispatchedAt</c> is indexed because the relay's only query is "the
/// undispatched ones"; <c>OrganizationId</c> is nullable and stays so, since a platform-wide event
/// belongs to no tenant.
/// </summary>
public sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    private const int TopicMaximumLength = 200;
    private const int PartitionKeyMaximumLength = 200;

    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(outboxMessage => outboxMessage.Id);
        builder.Property(outboxMessage => outboxMessage.OrganizationId);
        builder.Property(outboxMessage => outboxMessage.Topic)
            .IsRequired()
            .HasMaxLength(TopicMaximumLength);
        builder.Property(outboxMessage => outboxMessage.PartitionKey)
            .IsRequired()
            .HasMaxLength(PartitionKeyMaximumLength);
        builder.Property(outboxMessage => outboxMessage.Payload)
            .IsRequired();
        builder.Property(outboxMessage => outboxMessage.OccurredAt)
            .IsRequired();
        builder.HasIndex(outboxMessage => outboxMessage.DispatchedAt);
    }
}
