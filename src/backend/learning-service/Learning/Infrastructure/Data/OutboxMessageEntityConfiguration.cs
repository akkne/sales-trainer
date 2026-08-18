using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.BuildingBlocks.Outbox;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps the transactional outbox table.
///
/// <para>
/// The organization column is recorded but <b>deliberately not filtered and not required</b>: the relay
/// is a platform-wide pump that has to drain every organization's staged messages, and a message staged
/// in system mode legitimately has no owner. The index is on the dispatch timestamp, because the only
/// hot query is "what has not been dispatched yet".
/// </para>
/// </summary>
public sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(outboxMessage => outboxMessage.Id);
        builder.Property(outboxMessage => outboxMessage.OrganizationId);
        builder.Property(outboxMessage => outboxMessage.Topic)
            .IsRequired()
            .HasMaxLength(200);
        builder.Property(outboxMessage => outboxMessage.PartitionKey)
            .IsRequired()
            .HasMaxLength(200);
        builder.Property(outboxMessage => outboxMessage.Payload)
            .IsRequired();
        builder.Property(outboxMessage => outboxMessage.OccurredAt)
            .IsRequired();
        builder.HasIndex(outboxMessage => outboxMessage.DispatchedAt);
    }
}
